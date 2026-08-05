using System.Collections.Generic;
using Topomatic.Cad.Foundation;
using Topomatic.Dwg;
using Topomatic.Dwg.Entities;

namespace AbrRunoff.Report
{
    internal static class ReportTablePlacer
    {
        internal static void Place(DwgBlock space, Vector2D origin, IList<ReportRow> rows, double textH)
        {
            var cells = new List<string[]> { RunoffReport.Header() };
            foreach (var r in rows) cells.Add(RunoffReport.ToCells(r));

            int cols = cells[0].Length;
            var colW = new double[cols];
            for (int c = 0; c < cols; c++)
            {
                double w = 0;
                foreach (var row in cells)
                {
                    double cw = (row[c] ?? "").Length * textH * 0.62 + textH;
                    if (cw > w) w = cw;
                }
                colW[c] = w;
            }
            double rowH = textH * 1.8;

            double totalW = 0; foreach (var w in colW) totalW += w;
            double totalH = rowH * cells.Count;

            for (int r = 0; r <= cells.Count; r++)
            {
                double y = origin.Y - r * rowH;
                var line = new DwgPolyline();
                line.Add(new BugleVector2D(new Vector2D(origin.X, y)));
                line.Add(new BugleVector2D(new Vector2D(origin.X + totalW, y)));
                space.Add(line);
            }
            double x = origin.X;
            for (int c = 0; c <= cols; c++)
            {
                var line = new DwgPolyline();
                line.Add(new BugleVector2D(new Vector2D(x, origin.Y)));
                line.Add(new BugleVector2D(new Vector2D(x, origin.Y - totalH)));
                space.Add(line);
                if (c < cols) x += colW[c];
            }

            for (int r = 0; r < cells.Count; r++)
            {
                x = origin.X;
                for (int c = 0; c < cols; c++)
                {
                    var text = new DwgText();
                    text.Content = cells[r][c];   // свойство Content, не Value
                    text.Height = textH;
                    text.Position = new Vector3D(x + textH * 0.5, origin.Y - r * rowH - rowH + textH * 0.4, 0.0);
                    space.Add(text);
                    x += colW[c];
                }
            }
        }
    }
}
