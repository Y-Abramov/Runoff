using System;
using System.Collections.Generic;
using System.Globalization;
using AbrRunoff.Core;
using AbrRunoff.Core.Network;
using Topomatic.Cad.Foundation;
using Topomatic.Dwg.Entities;

namespace AbrRunoff.Entity
{
    /// <summary>
    /// Таблица бассейнов на плане: образец цвета, номер, куда сбрасывает, длина
    /// сети, число узлов, статус.
    ///
    /// Отдельно от RunoffLegend сознательно. Легенда объясняет ЗНАКИ и одинакова
    /// в любом проекте; эта таблица - сводка ПО КОНКРЕТНОЙ сети, ради которой
    /// схему и открывают: сколько бассейнов, куда каждый уходит и где беда.
    /// Проблемные строки идут первыми и красным.
    /// </summary>
    internal static class BasinTable
    {
        internal static void Build(Vector2D origin, IList<BasinInfo> basins, RunoffSettings settings,
                                   Action<DwgEntity> emit)
        {
            if (basins == null || basins.Count == 0) return;

            double h = settings.TextHeight;
            double rowH = h * 2.0;
            double swatchW = h * 2.2;
            double pad = h * 0.6;

            var rows = new List<string[]>();
            rows.Add(new[] { "Бассейн", "Сброс", "Длина, м", "Узлов", "Статус" });

            var ordered = new List<BasinInfo>(basins);
            // Проблемные первыми - таблицу читают ради них.
            ordered.Sort(delegate (BasinInfo a, BasinInfo b)
            {
                if (a.IsProblem != b.IsProblem) return a.IsProblem ? -1 : 1;
                return a.Number.CompareTo(b.Number);
            });

            foreach (var b in ordered) rows.Add(Cells(b));

            // Ширины колонок по содержимому: узкая таблица с обрезанным текстом
            // хуже широкой.
            int cols = rows[0].Length;
            var colW = new double[cols];
            for (int c = 0; c < cols; c++)
            {
                double w = 0.0;
                foreach (var r in rows)
                {
                    double cw = (r[c] ?? "").Length * h * RunoffStyle.CharWidthRatio + pad * 2.0;
                    if (cw > w) w = cw;
                }
                colW[c] = w;
            }

            double totalW = swatchW;
            foreach (var w in colW) totalW += w;
            double totalH = rowH * rows.Count;

            // Подложка таблицы - белая и НЕПРОЗРАЧНАЯ: это врезка-документ поверх
            // плана, а не зона на местности. Здесь непрозрачность уместна.
            GlyphBuilder.Fill(new[]
            {
                new Vector2D(origin.X, origin.Y),
                new Vector2D(origin.X + totalW, origin.Y),
                new Vector2D(origin.X + totalW, origin.Y - totalH),
                new Vector2D(origin.X, origin.Y - totalH)
            }, RunoffStyle.Paper, emit);

            EmitGrid(origin, colW, swatchW, rowH, totalW, totalH, rows.Count, cols, emit);

            for (int r = 0; r < rows.Count; r++)
            {
                double yTop = origin.Y - r * rowH;
                double yText = yTop - rowH * 0.5;
                bool header = r == 0;
                var info = header ? null : ordered[r - 1];

                if (!header) EmitSwatch(origin.X, yTop, swatchW, rowH, info, settings, emit);

                double x = origin.X + swatchW;
                for (int c = 0; c < cols; c++)
                {
                    var color = header ? RunoffStyle.Normal
                              : info.IsProblem ? RunoffStyle.Error : RunoffStyle.Muted;

                    double unused;
                    GlyphBuilder.Text(rows[r][c], new Vector2D(x + pad, yText), h, 0.0, color, emit, out unused);
                    x += colW[c];
                }
            }
        }

        /// <summary>Образец цвета бассейна - тем же способом, каким закрашена зона на плане.</summary>
        private static void EmitSwatch(double x0, double yTop, double swatchW, double rowH,
                                       BasinInfo info, RunoffSettings settings, Action<DwgEntity> emit)
        {
            double inset = rowH * 0.22;
            var box = new[]
            {
                new Vector2D(x0 + inset, yTop - inset),
                new Vector2D(x0 + swatchW - inset, yTop - inset),
                new Vector2D(x0 + swatchW - inset, yTop - rowH + inset),
                new Vector2D(x0 + inset, yTop - rowH + inset)
            };

            var color = BasinPalette.For(info.Index);

            if (settings.BasinZone == BasinZoneStyle.Hatched)
                GlyphBuilder.ZoneHatched(box, color, BasinPalette.HatchAngle(info.Index), rowH * 0.4, emit);
            else
                GlyphBuilder.Zone(box, color, Math.Max(settings.BasinZoneOpacity, 35.0), emit);

            GlyphBuilder.Outline(box, info.IsProblem ? RunoffStyle.Error : color,
                                 info.IsProblem ? RunoffStyle.LineHeavy : RunoffStyle.LineThin, true, emit);
        }

        private static void EmitGrid(Vector2D origin, double[] colW, double swatchW, double rowH,
                                     double totalW, double totalH, int rowCount, int cols,
                                     Action<DwgEntity> emit)
        {
            for (int r = 0; r <= rowCount; r++)
            {
                double y = origin.Y - r * rowH;
                GlyphBuilder.Line(new Vector2D(origin.X, y), new Vector2D(origin.X + totalW, y),
                                  RunoffStyle.Muted, RunoffStyle.LineThin, emit);
            }

            double x = origin.X;
            GlyphBuilder.Line(new Vector2D(x, origin.Y), new Vector2D(x, origin.Y - totalH),
                              RunoffStyle.Muted, RunoffStyle.LineThin, emit);
            x += swatchW;
            for (int c = 0; c <= cols; c++)
            {
                GlyphBuilder.Line(new Vector2D(x, origin.Y), new Vector2D(x, origin.Y - totalH),
                                  RunoffStyle.Muted, RunoffStyle.LineThin, emit);
                if (c < cols) x += colW[c];
            }
        }

        private static string[] Cells(BasinInfo b)
        {
            var ci = CultureInfo.InvariantCulture;
            return new[]
            {
                b.Number.ToString(ci),
                OutfallText(b),
                b.TotalLength.ToString("F0", ci),
                b.NodeCount.ToString(ci),
                b.IsProblem ? "НЕТ ВЫПУСКА" : "норма"
            };
        }

        private static string OutfallText(BasinInfo b)
        {
            if (!b.HasOutfall) return "-";
            string kind = b.OutfallKind == OutfallKind.StormWell ? "колодец"
                        : b.OutfallKind == OutfallKind.PipeOutward ? "труба"
                        : "сброс за трассу";
            return string.Format(CultureInfo.InvariantCulture, "{0} {1:F2}", kind, b.OutfallZ);
        }
    }
}
