using System;
using System.Collections.Generic;
using System.Globalization;
using AbrRunoff.Core.Network;

namespace AbrRunoff.Report
{
    public sealed class NetworkRow
    {
        public int NodeId;
        public string Source = "";
        public int Basin;                 // 1-based для человека; 0 = нет бассейна
        public string Outfall = "";
        public double TotalLength;
        public double WorstGradePermille;
        public string Confidence = "";
        public string Status = "";
        public bool IsProblem;
    }

    /// <summary>Строки ведомости по сети. Без Topomatic - линкуется в тесты.</summary>
    public static class NetworkReport
    {
        public static List<NetworkRow> Build(DrainageNetwork net, Dictionary<int, int> basins)
        {
            var rows = new List<NetworkRow>();
            if (net == null) return rows;

            var starts = new List<DrainageNode>();
            var covered = new HashSet<int>();

            // Начала цепочек: узлы без входящих рёбер. Промежуточные точки строк
            // не дают - они дублировали бы тот же путь.
            foreach (var node in net.Nodes)
                if (!HasIncoming(net, node.Id)) { starts.Add(node); covered.Add(node.Id); }

            // Кольцо не имеет начала: у КАЖДОГО его узла есть входящее ребро,
            // и по правилу выше оно целиком выпало бы из ведомости - ровно та
            // диагностика, ради которой циклы и ищутся. Берём по одному узлу
            // на каждую непокрытую компоненту.
            foreach (var node in net.Nodes)
            {
                if (covered.Contains(node.Id)) continue;
                if (node.Outfall != OutfallKind.None) continue;

                var trace = net.Trace(node.Id);
                if (trace.Outcome != TraceOutcome.Cycle) continue;

                starts.Add(node);
                covered.Add(node.Id);
                foreach (int edgeId in trace.Path)
                    foreach (var e in net.Edges)
                        if (e.Id == edgeId) { covered.Add(e.FromNode); covered.Add(e.ToNode); }
            }

            foreach (var node in starts)
            {
                var trace = net.Trace(node.Id);
                int basin;
                if (!basins.TryGetValue(node.Id, out basin)) basin = -1;

                var row = new NetworkRow
                {
                    NodeId = node.Id,
                    Source = node.SourceRef,
                    Basin = basin + 1,
                    TotalLength = trace.TotalLength,
                    WorstGradePermille = trace.WorstGradePermille == double.MaxValue
                                         ? 0.0 : trace.WorstGradePermille,
                    Confidence = ConfidenceName(trace.WorstConfidence)
                };

                switch (trace.Outcome)
                {
                    case TraceOutcome.ReachedOutfall:
                        var outfall = net.NodeById(trace.OutfallNode);
                        row.Outfall = OutfallName(outfall);
                        row.Status = trace.Splits ? "Сток разделяется" : "норма";
                        row.IsProblem = trace.Splits;
                        break;
                    case TraceOutcome.Cycle:
                        row.Outfall = "-";
                        row.Status = "Кольцевой сток, проверьте отметки";
                        row.IsProblem = true;
                        break;
                    default:
                        row.Outfall = "-";
                        row.Status = "Сток не доходит до выпуска";
                        row.IsProblem = true;
                        break;
                }
                rows.Add(row);
            }

            // Проблемные строки первыми: ведомость читают ради них.
            rows.Sort(delegate (NetworkRow a, NetworkRow b)
            {
                if (a.IsProblem != b.IsProblem) return a.IsProblem ? -1 : 1;
                int byBasin = a.Basin.CompareTo(b.Basin);
                if (byBasin != 0) return byBasin;
                return string.Compare(a.Source, b.Source, StringComparison.OrdinalIgnoreCase);
            });
            return rows;
        }

        private static bool HasIncoming(DrainageNetwork net, int nodeId)
        {
            foreach (var e in net.Edges) if (e.ToNode == nodeId) return true;
            return false;
        }

        private static string ConfidenceName(LinkConfidence c)
        {
            return c == LinkConfidence.Explicit ? "из модели"
                 : c == LinkConfidence.Physical ? "по трубе"
                 : "предположение";
        }

        private static string OutfallName(DrainageNode n)
        {
            if (n == null) return "-";
            string kind = n.Outfall == OutfallKind.StormWell ? "колодец"
                        : n.Outfall == OutfallKind.PipeOutward ? "труба"
                        : "сброс за трассу";
            return string.Format(CultureInfo.InvariantCulture, "{0} {1:F2}", kind, n.Z);
        }

        public static string[] Header()
        {
            return new[] { "Источник", "Бассейн", "Сброс", "Длина до сброса, м",
                           "Худший уклон, промилле", "Достоверность", "Статус" };
        }

        public static string[] ToCells(NetworkRow r)
        {
            var ci = CultureInfo.InvariantCulture;
            return new[]
            {
                r.Source,
                r.Basin > 0 ? r.Basin.ToString(ci) : "-",
                r.Outfall,
                r.TotalLength.ToString("F2", ci),
                r.WorstGradePermille.ToString("F2", ci),
                r.Confidence,
                r.Status
            };
        }
    }
}
