using System;
using System.Collections.Generic;

namespace AbrRunoff.Core.Network
{
    /// <summary>
    /// Сшивка элементов в сеть. Три прохода по убыванию достоверности - порядок
    /// обязателен: явная связь должна выиграть у геометрической догадки на том же
    /// стыке, иначе модуль будет утверждать «наверное» там, где в проекте есть «точно».
    ///
    /// Вызывающий обязан соблюдать порядок: LinkExplicit -> LinkPipe -> LinkInferred.
    /// </summary>
    public static class NetworkLinker
    {
        /// <summary>Связь взята из модели напрямую (NodeConnectedDitch.ConnectedNode).</summary>
        public static void LinkExplicit(DrainageNetwork net, int fromNode, int toNode, ElementKind element)
        {
            AddEdge(net, fromNode, toNode, element, LinkConfidence.Explicit, 0.0, 0.0);
        }

        /// <summary>Труба: длина и уклон известны из её собственной геометрии.</summary>
        public static void LinkPipe(DrainageNetwork net, int fromNode, int toNode,
                                    double length, double gradePermille)
        {
            AddEdge(net, fromNode, toNode, ElementKind.Pipe, LinkConfidence.Physical, length, gradePermille);
        }

        /// <summary>
        /// Геометрическая догадка для оставшихся открытых концов.
        /// Соединяется только вниз по отметке: вода не течёт вверх.
        /// </summary>
        public static void LinkInferred(DrainageNetwork net, double tolerance)
        {
            var index = new SpatialIndex(Math.Max(tolerance, 1.0));
            foreach (var n in net.Nodes) index.Add(n.Id, n.X, n.Y);

            var byId = new Dictionary<int, DrainageNode>(net.Nodes.Count);
            foreach (var n in net.Nodes) byId[n.Id] = n;

            // Узлы, у которых уже есть исходящее ребро, второй раз не связываем:
            // сток из точки уже определён более достоверным проходом.
            var hasOutgoing = new HashSet<int>();
            foreach (var e in net.Edges) hasOutgoing.Add(e.FromNode);

            var fresh = new List<DrainageEdge>();
            foreach (var node in net.Nodes)
            {
                if (hasOutgoing.Contains(node.Id)) continue;
                if (node.Outfall != OutfallKind.None) continue;

                DrainageNode best = null;
                double bestDist = double.MaxValue;

                foreach (int id in index.Query(node.X, node.Y, tolerance))
                {
                    if (id == node.Id) continue;
                    var cand = byId[id];

                    // Вода не течёт вверх - главный фильтр ложных сшивок.
                    if (cand.Z >= node.Z) continue;

                    double dx = cand.X - node.X, dy = cand.Y - node.Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist >= bestDist) continue;

                    bestDist = dist;
                    best = cand;
                }

                if (best == null) continue;

                double grade = bestDist > 1e-9 ? (node.Z - best.Z) / bestDist * 1000.0 : 0.0;
                fresh.Add(new DrainageEdge
                {
                    Id = 0,
                    FromNode = node.Id, ToNode = best.Id,
                    Length = bestDist, GradePermille = grade,
                    Element = ElementKind.SiteLine,
                    Confidence = LinkConfidence.Inferred
                });
            }

            foreach (var e in fresh) AppendEdge(net, e);
            net.InvalidateIndex();
        }

        private static void AddEdge(DrainageNetwork net, int from, int to, ElementKind element,
                                    LinkConfidence conf, double length, double grade)
        {
            AppendEdge(net, new DrainageEdge
            {
                FromNode = from, ToNode = to, Element = element,
                Confidence = conf, Length = length, GradePermille = grade
            });
            net.InvalidateIndex();
        }

        private static void AppendEdge(DrainageNetwork net, DrainageEdge edge)
        {
            int maxId = 0;
            foreach (var e in net.Edges) if (e.Id > maxId) maxId = e.Id;
            edge.Id = maxId + 1;
            net.Edges.Add(edge);
        }
    }
}
