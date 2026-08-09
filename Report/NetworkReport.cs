using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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

        // Координаты плана для зума по двойному клику. У сети нет пикетажа
        // (элементы приходят из разных моделей), поэтому зумить можно только
        // по координате - в отличие от ведомости стока, где есть дорога и ПК.
        public double X, Y;                       // начало цепочки
        public double OutfallX, OutfallY;         // выпуск, если цепочка до него дошла
        public bool HasOutfallPos;
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
                    Confidence = ConfidenceName(trace.WorstConfidence),
                    X = node.X,
                    Y = node.Y
                };

                switch (trace.Outcome)
                {
                    case TraceOutcome.ReachedOutfall:
                        var outfall = net.NodeById(trace.OutfallNode);
                        row.Outfall = OutfallName(outfall);
                        row.Status = trace.Splits ? "Сток разделяется" : "норма";
                        row.IsProblem = trace.Splits;
                        if (outfall != null)
                        {
                            row.OutfallX = outfall.X;
                            row.OutfallY = outfall.Y;
                            row.HasOutfallPos = true;
                        }
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

        /// <summary>
        /// Строка ведомости для трубы, которая не вошла в сеть или вошла с изъяном.
        ///
        /// Труба, молча выпавшая из расчёта, - худшее поведение: проектировщик
        /// решит, что она учтена. Поэтому у неё своя строка, помеченная проблемой,
        /// с координатой для зума. Бассейна у такой трубы нет - в колонке прочерк.
        /// </summary>
        public static NetworkRow PipeProblemRow(string source, string status, double x, double y)
        {
            return new NetworkRow
            {
                NodeId = -1,
                Source = source ?? "труба",
                Basin = 0,                 // 0 -> в ячейке «-», см. ToCells
                Outfall = "-",
                TotalLength = 0.0,
                WorstGradePermille = 0.0,
                Confidence = "-",
                Status = status,
                IsProblem = true,
                X = x,
                Y = y,
                HasOutfallPos = false
            };
        }

        // Выгрузка - тем же форматом, что ведомость стока: инструмент один,
        // и файлы из обеих ведомостей должны открываться одинаково.

        public static string ToCsv(IList<NetworkRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(";", Escape(Header())));
            foreach (var r in rows)
                sb.AppendLine(string.Join(";", Escape(ToCells(r))));
            return sb.ToString();
        }

        public static string ToHtml(IList<NetworkRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!doctype html><meta charset=\"utf-8\"><title>Ведомость по бассейнам</title>");
            sb.AppendLine("<style>body{font:14px Segoe UI,sans-serif}table{border-collapse:collapse}" +
                          "td,th{border:1px solid #ccc;padding:4px 8px}tr.p{background:#fde8e6}</style>");
            sb.AppendLine("<h1>Ведомость по бассейнам</h1><table><tr>");
            foreach (var h in Header()) sb.Append("<th>").Append(HtmlEscape(h)).Append("</th>");
            sb.AppendLine("</tr>");
            foreach (var r in rows)
            {
                sb.Append(r.IsProblem ? "<tr class=\"p\">" : "<tr>");
                foreach (var c in ToCells(r)) sb.Append("<td>").Append(HtmlEscape(c)).Append("</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</table>");
            return sb.ToString();
        }

        private static string[] Escape(string[] cells)
        {
            var res = new string[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                string c = cells[i] ?? "";
                res[i] = c.IndexOf(';') >= 0 || c.IndexOf('"') >= 0
                    ? "\"" + c.Replace("\"", "\"\"") + "\""
                    : c;
            }
            return res;
        }

        private static string HtmlEscape(string s)
        {
            return (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
