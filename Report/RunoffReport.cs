using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AbrRunoff.Core;

namespace AbrRunoff.Report
{
    public sealed class ReportRow
    {
        public string Road;
        public string Side;
        public double StationFrom;
        public double StationTo;
        public double Length;
        public string Direction;
        public double GradePermille;
        public string Outflow;
        public string Status;
        public bool IsProblem;
    }

    /// <summary>Строки ведомости. Без Topomatic - линкуется в тесты.</summary>
    public static class RunoffReport
    {
        public static List<ReportRow> Build(string roadName, RunoffResult result)
        {
            var rows = new List<ReportRow>();
            if (result == null) return rows;

            foreach (var seg in result.Segments)
            {
                bool zero = (seg.Flags & SegmentFlags.ZeroGrade) != 0;
                bool below = (seg.Flags & SegmentFlags.BelowNorm) != 0;

                rows.Add(new ReportRow
                {
                    Road          = roadName,
                    Side          = seg.Side == DitchSide.Left ? "лево" : "право",
                    StationFrom   = seg.StationFrom,
                    StationTo     = seg.StationTo,
                    Length        = seg.Length,
                    Direction     = seg.Direction == FlowDirection.Forward ? "по пикетажу"
                                  : seg.Direction == FlowDirection.Backward ? "против пикетажа" : "нет",
                    GradePermille = seg.AvgGradePermille,
                    Outflow       = OutflowFor(seg, result),
                    Status        = zero ? "нулевой уклон" : below ? "уклон ниже нормы" : "норма",
                    IsProblem     = zero || below
                });
            }

            foreach (var pt in result.Points)
            {
                if (pt.Kind != PointKind.Collector) continue;
                rows.Add(new ReportRow
                {
                    Road        = roadName,
                    Side        = pt.Side == DitchSide.Left ? "лево" : "право",
                    StationFrom = pt.Station,
                    StationTo   = pt.Station,
                    Length      = 0,
                    Direction   = "точка сбора",
                    Outflow     = pt.PipeStation.HasValue
                                  ? string.Format(CultureInfo.InvariantCulture, "труба ПК{0:F0}", pt.PipeStation.Value)
                                  : "нет выпуска",
                    Status      = pt.Problem == ProblemKind.NoOutlet ? "бессточное понижение" : "норма",
                    IsProblem   = pt.Problem == ProblemKind.NoOutlet
                });
            }

            // Проблемные строки первыми: ведомость читают ради них.
            rows.Sort(delegate (ReportRow a, ReportRow b)
            {
                if (a.IsProblem != b.IsProblem) return a.IsProblem ? -1 : 1;
                int byRoad = string.Compare(a.Road, b.Road, StringComparison.OrdinalIgnoreCase);
                if (byRoad != 0) return byRoad;
                int bySide = string.Compare(a.Side, b.Side, StringComparison.Ordinal);
                if (bySide != 0) return bySide;
                return a.StationFrom.CompareTo(b.StationFrom);
            });

            return rows;
        }

        private static string OutflowFor(FlowSegment seg, RunoffResult result)
        {
            // Куда уходит вода этого участка: точка на том конце, к которому она течёт.
            double target = seg.Direction == FlowDirection.Backward ? seg.StationFrom : seg.StationTo;
            if (seg.Direction == FlowDirection.None) return "стоит";

            foreach (var pt in result.Points)
            {
                if (pt.Side != seg.Side || Math.Abs(pt.Station - target) > 1e-6) continue;
                switch (pt.Kind)
                {
                    case PointKind.Collector:
                        return pt.PipeStation.HasValue
                            ? string.Format(CultureInfo.InvariantCulture, "сбор ПК{0:F0}, труба", target)
                            : string.Format(CultureInfo.InvariantCulture, "сбор ПК{0:F0}, нет выпуска", target);
                    case PointKind.Outlet:
                        return string.Format(CultureInfo.InvariantCulture, "сброс ПК{0:F0}", target);
                }
            }
            return string.Format(CultureInfo.InvariantCulture, "ПК{0:F0}", target);
        }

        public static string[] Header()
        {
            return new[] { "Дорога", "Сторона", "ПК от", "ПК до", "Длина, м",
                           "Направление", "Уклон, промилле", "Сброс", "Статус" };
        }

        public static string[] ToCells(ReportRow r)
        {
            var ci = CultureInfo.InvariantCulture;
            return new[]
            {
                r.Road, r.Side,
                r.StationFrom.ToString("F2", ci), r.StationTo.ToString("F2", ci),
                r.Length.ToString("F2", ci), r.Direction,
                r.GradePermille.ToString("F2", ci), r.Outflow, r.Status
            };
        }

        public static string ToCsv(IList<ReportRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(";", Escape(Header())));
            foreach (var r in rows)
                sb.AppendLine(string.Join(";", Escape(ToCells(r))));
            return sb.ToString();
        }

        public static string ToHtml(IList<ReportRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!doctype html><meta charset=\"utf-8\"><title>Ведомость участков стока</title>");
            sb.AppendLine("<style>body{font:14px Segoe UI,sans-serif}table{border-collapse:collapse}" +
                          "td,th{border:1px solid #ccc;padding:4px 8px}tr.p{background:#fde8e6}</style>");
            sb.AppendLine("<h1>Ведомость участков стока</h1><table><tr>");
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
