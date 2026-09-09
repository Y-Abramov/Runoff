using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AbrRunoff.Core.Watershed;

namespace AbrRunoff.Report
{
    public sealed class WatershedRow
    {
        public int Number;
        public string Outlet;
        public double AreaKm2;
        public double AreaHa;
        public double LengthKm;
        public double EndSlope;
        public double WeightedSlope;
        public double HeadZ;
        public double OutletZ;
        public double MeanBasinSlope;
        public string Status;
        public bool IsProblem;

        /// <summary>Исходный результат - для окна ведомости (профиль лога и т.п.), не для экспорта.</summary>
        public WatershedResult Source;
    }

    /// <summary>Строки ведомости водосборов. Без Topomatic - линкуется в тесты.</summary>
    public static class WatershedReport
    {
        public static List<WatershedRow> Build(IList<WatershedResult> results)
        {
            var ordered = new List<WatershedResult>(results);
            // Номер - от положения створа, а не от порядка построения: перестроение
            // не должно перенумеровывать водосборы и сбивать проектировщику ориентацию.
            ordered.Sort((a, b) =>
            {
                int c = a.OutletPoint.X.CompareTo(b.OutletPoint.X);
                return c != 0 ? c : a.OutletPoint.Y.CompareTo(b.OutletPoint.Y);
            });

            var rows = new List<WatershedRow>();
            for (int i = 0; i < ordered.Count; i++)
            {
                var r = ordered[i];
                rows.Add(new WatershedRow
                {
                    Number = i + 1,
                    Outlet = r.OutletName,
                    AreaKm2 = r.AreaKm2,
                    AreaHa = r.AreaM2 / 10000.0,
                    LengthKm = r.LengthM / 1000.0,
                    EndSlope = r.EndSlope,
                    WeightedSlope = r.WeightedSlope,
                    HeadZ = r.HeadZ,
                    OutletZ = r.OutletZ,
                    MeanBasinSlope = r.MeanBasinSlope,
                    Status = string.Join("; ", r.Statuses),
                    IsProblem = r.HasProblem,
                    Source = r
                });
            }

            // Проблемные строки первыми: ведомость читают ради них. Внутри каждой
            // группы - устойчивый порядок по номеру (то есть по координате створа).
            rows.Sort((a, b) => a.IsProblem == b.IsProblem
                ? a.Number.CompareTo(b.Number)
                : (a.IsProblem ? -1 : 1));
            return rows;
        }

        public static string[] Header()
        {
            return new[] { "№", "Створ", "F, км²", "F, га", "L, км", "i (по концам)",
                           "i (средневзвеш.)", "Hисток, м", "Hствор, м", "Ср. уклон водосбора", "Статус" };
        }

        public static string[] ToCells(WatershedRow r)
        {
            var ci = CultureInfo.InvariantCulture;
            return new[]
            {
                r.Number.ToString(ci),
                r.Outlet,
                r.AreaKm2.ToString("F3", ci),
                r.AreaHa.ToString("F1", ci),
                r.LengthKm.ToString("F3", ci),
                r.EndSlope.ToString("F4", ci),
                r.WeightedSlope.ToString("F4", ci),
                r.HeadZ.ToString("F2", ci),
                r.OutletZ.ToString("F2", ci),
                r.MeanBasinSlope.ToString("F4", ci),
                r.Status
            };
        }

        public static string ToCsv(IList<WatershedRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(";", Escape(Header())));
            foreach (var r in rows)
                sb.AppendLine(string.Join(";", Escape(ToCells(r))));
            return sb.ToString();
        }

        public static string ToHtml(IList<WatershedRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!doctype html><meta charset=\"utf-8\"><title>Ведомость водосборов</title>");
            sb.AppendLine("<style>body{font:14px Segoe UI,sans-serif}table{border-collapse:collapse}" +
                          "td,th{border:1px solid #ccc;padding:4px 8px}tr.p{background:#fde8e6}</style>");
            sb.AppendLine("<h1>Ведомость водосборов</h1><table><tr>");
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
