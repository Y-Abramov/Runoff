using System;
using System.Collections.Generic;
using AbrRunoff.Core;
using AbrRunoff.Core.Network;
using AbrRunoff.Robur.Sources;
using Topomatic.Cad.Foundation;

namespace AbrRunoff.Robur
{
    /// <summary>
    /// Собирает граф сети из элементов источников.
    ///
    /// Логика «где участок, где экстремум» целиком остаётся за FlowDetector -
    /// здесь только перевод его результата в узлы и рёбра плюс сшивка.
    /// </summary>
    internal static class NetworkBuilder
    {
        internal static DrainageNetwork Build(IList<DrainageElement> elements,
                                              IList<PipeEdgeInfo> pipes,
                                              RunoffSettings settings,
                                              double linkTolerance)
        {
            var net = new DrainageNetwork();
            int nextNodeId = 1;

            foreach (var el in elements)
            {
                var result = FlowDetector.Detect(new RunoffInput
                {
                    Left = el.Samples,
                    Right = new List<DitchSample>(),
                    Settings = settings
                });

                // Узел на каждую характерную станцию элемента.
                var nodeAt = new Dictionary<double, int>();
                foreach (var seg in result.Segments)
                {
                    EnsureNode(net, nodeAt, ref nextNodeId, el, seg.StationFrom, NodeKind.SegmentEnd);
                    EnsureNode(net, nodeAt, ref nextNodeId, el, seg.StationTo, NodeKind.SegmentEnd);
                }
                foreach (var pt in result.Points)
                {
                    var kind = pt.Kind == PointKind.Watershed ? NodeKind.Watershed
                             : pt.Kind == PointKind.Collector ? NodeKind.Collector
                             : NodeKind.SegmentEnd;
                    int id = EnsureNode(net, nodeAt, ref nextNodeId, el, pt.Station, kind);

                    // Сброс за пределы элемента - законный конец сети.
                    if (pt.Kind == PointKind.Outlet)
                        net.NodeById(id).Outfall = OutfallKind.BeyondAlignment;
                }

                foreach (var seg in result.Segments)
                {
                    if (seg.Direction == FlowDirection.None) continue;

                    int a = nodeAt[seg.StationFrom];
                    int b = nodeAt[seg.StationTo];

                    // Backward = вода идёт против роста станции: разворачиваем ребро,
                    // потому что ребро в графе ВСЕГДА направлено по течению.
                    int from = seg.Direction == FlowDirection.Forward ? a : b;
                    int to = seg.Direction == FlowDirection.Forward ? b : a;

                    net.Edges.Add(new DrainageEdge
                    {
                        Id = net.Edges.Count + 1,
                        FromNode = from, ToNode = to,
                        Length = seg.Length,
                        GradePermille = seg.AvgGradePermille,
                        Element = el.Kind,
                        Confidence = LinkConfidence.Explicit,   // внутри элемента связь достоверна
                        SourceRef = el.SourceRef
                    });
                }
            }
            net.InvalidateIndex();

            // Проход 2: трубы. Проход 1 (явные связи) выполняет вызывающий -
            // он знает соответствие кюветов сетей узлам ливневки.
            foreach (var pipe in pipes) AddPipe(net, ref nextNodeId, pipe, linkTolerance);
            net.InvalidateIndex();

            // Проход 3: геометрические догадки по остаткам.
            NetworkLinker.LinkInferred(net, linkTolerance);
            return net;
        }

        private static int EnsureNode(DrainageNetwork net, Dictionary<double, int> nodeAt,
                                      ref int nextId, DrainageElement el, double station, NodeKind kind)
        {
            int existing;
            if (nodeAt.TryGetValue(station, out existing)) return existing;

            var pos = PositionAt(el, station);
            var node = new DrainageNode
            {
                Id = nextId++,
                X = pos.X, Y = pos.Y,
                Z = ElevationAt(el, station),
                Kind = kind,
                SourceRef = el.SourceRef
            };
            net.Nodes.Add(node);
            // Индекс NodeById кэшируется лениво и не знает про этот Add - без
            // сброса второй и далее узел с Outlet (см. вызов ниже) не найдётся:
            // NodeById(id) вернёт null -> NullReferenceException на .Outfall.
            net.InvalidateIndex();
            nodeAt[station] = node.Id;
            return node.Id;
        }

        private static Vector2D PositionAt(DrainageElement el, double station)
        {
            int best = -1;
            double bestDist = double.MaxValue;
            for (int i = 0; i < el.Samples.Count; i++)
            {
                double d = Math.Abs(el.Samples[i].Station - station);
                if (d >= bestDist) continue;
                bestDist = d; best = i;
            }
            return best >= 0 && best < el.Positions.Count ? el.Positions[best] : Vector2D.Empty;
        }

        private static double ElevationAt(DrainageElement el, double station)
        {
            double best = 0.0, bestDist = double.MaxValue;
            foreach (var s in el.Samples)
            {
                if (!s.IsDitch) continue;
                double d = Math.Abs(s.Station - station);
                if (d >= bestDist) continue;
                bestDist = d; best = s.BottomZ;
            }
            return best;
        }

        private static void AddPipe(DrainageNetwork net, ref int nextId, PipeEdgeInfo pipe, double tolerance)
        {
            // Вода течёт от конца с большей отметкой к меньшей.
            var upper = pipe.StartZ >= pipe.EndZ ? pipe.Start : pipe.End;
            var lower = pipe.StartZ >= pipe.EndZ ? pipe.End : pipe.Start;
            double upperZ = Math.Max(pipe.StartZ, pipe.EndZ);
            double lowerZ = Math.Min(pipe.StartZ, pipe.EndZ);

            int fromNode = NearestNode(net, upper, tolerance);
            if (fromNode < 0) return;   // труба ни к чему не примыкает - в сеть не входит

            int toNode = NearestNode(net, lower, tolerance);
            if (toNode < 0)
            {
                // Дальний конец никуда не примыкает - значит труба уводит воду
                // за пределы земполотна. Это законный выпуск.
                var outfall = new DrainageNode
                {
                    Id = nextId++,
                    X = lower.X, Y = lower.Y, Z = lowerZ,
                    Kind = NodeKind.PipeMouth,
                    Outfall = OutfallKind.PipeOutward,
                    SourceRef = pipe.SourceRef
                };
                net.Nodes.Add(outfall);
                net.InvalidateIndex();
                toNode = outfall.Id;
            }

            double dx = lower.X - upper.X, dy = lower.Y - upper.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            double grade = len > 1e-9 ? (upperZ - lowerZ) / len * 1000.0 : 0.0;

            NetworkLinker.LinkPipe(net, fromNode, toNode, len, grade);
        }

        private static int NearestNode(DrainageNetwork net, Vector2D at, double tolerance)
        {
            int best = -1;
            double bestDist = tolerance;
            foreach (var n in net.Nodes)
            {
                double dx = n.X - at.X, dy = n.Y - at.Y;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d > bestDist) continue;
                bestDist = d; best = n.Id;
            }
            return best;
        }
    }

    /// <summary>Труба, приведённая к геометрии плана. Заполняется в Task 7 из Alignment.Pipes.</summary>
    internal sealed class PipeEdgeInfo
    {
        public Vector2D Start, End;
        public double StartZ, EndZ;
        public double Diameter;
        public string SourceRef = "";
    }
}
