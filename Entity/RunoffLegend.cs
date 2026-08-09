using System;
using System.Collections.Generic;
using AbrRunoff.Core;
using Topomatic.Cad.Foundation;
using Topomatic.Dwg.Entities;

namespace AbrRunoff.Entity
{
    /// <summary>
    /// Легенда схемы стока: образец знака плюс расшифровка.
    ///
    /// Строится теми же примитивами, что и сама схема (GlyphBuilder), поэтому
    /// образец в легенде не может разъехаться с тем, что нарисовано на плане -
    /// нарисованная отдельно «картинка легенды» рано или поздно устарела бы.
    /// </summary>
    internal static class RunoffLegend
    {
        internal static void Build(Vector2D origin, RunoffSettings settings, Action<DwgEntity> emit)
        {
            double g = RunoffStyle.GlyphBase * settings.GlyphScale;
            double h = settings.TextHeight;
            double rowH = Math.Max(g * 2.2, h * 2.4);
            double sampleX = origin.X + g * 2.0;
            double textX = origin.X + g * 4.6;
            double width = Math.Max(g * 4.6 + h * 26.0 * RunoffStyle.CharWidthRatio, g * 20.0);

            var rows = new List<Action<double>>();
            var right = new Vector2D(1.0, 0.0);

            // ── линии участков ─────────────────────────────────────────────
            rows.Add(y => LineRow(sampleX, y, g, RunoffStyle.Normal, RunoffStyle.LineMain, emit));
            rows.Add(y => LineRow(sampleX, y, g, RunoffStyle.Warning, RunoffStyle.LineHeavy, emit));
            rows.Add(y => LineRow(sampleX, y, g, RunoffStyle.Error, RunoffStyle.LineHeavy, emit));

            // ── знаки ──────────────────────────────────────────────────────
            rows.Add(y =>
            {
                var p = new Vector2D(sampleX, y);
                GlyphBuilder.Line(new Vector2D(p.X - g, p.Y), p, RunoffStyle.Normal, RunoffStyle.LineMain, emit);
                GlyphBuilder.ArrowHead(new Vector2D(p.X + g * 0.4, p.Y), right, g * 0.8, RunoffStyle.Normal, emit);
            });
            rows.Add(y => WatershedSample(new Vector2D(sampleX, y), g, emit));
            rows.Add(y => CollectorSample(new Vector2D(sampleX, y), g, false, emit));
            rows.Add(y => CollectorSample(new Vector2D(sampleX, y), g, true, emit));
            rows.Add(y => OutletSample(new Vector2D(sampleX, y), g, emit));
            rows.Add(y => StagnantSample(new Vector2D(sampleX, y), g, emit));

            // ── сеть водоотвода ────────────────────────────────────────────
            rows.Add(y =>
            {
                var p = new Vector2D(sampleX, y);
                GlyphBuilder.Line(new Vector2D(p.X - g * 1.2, p.Y),
                                  new Vector2D(p.X + g * 1.2, p.Y),
                                  BasinPalette.For(0), RunoffStyle.LineMain, emit);
            });
            rows.Add(y =>
            {
                // Пунктир - образец предположенной связи.
                var p = new Vector2D(sampleX, y);
                for (double t = -1.2; t < 1.2; t += 0.8)
                    GlyphBuilder.Line(new Vector2D(p.X + g * t, p.Y),
                                      new Vector2D(p.X + g * (t + 0.4), p.Y),
                                      RunoffStyle.Muted, RunoffStyle.LineMain, emit);
            });
            rows.Add(y =>
            {
                var p = new Vector2D(sampleX, y);
                GlyphBuilder.Disc(p, g * 0.5, BasinPalette.For(1), emit);
                GlyphBuilder.Circle(p, g * 0.85, BasinPalette.For(1), RunoffStyle.LineHeavy, emit);
            });

            var captions = new[]
            {
                "уклон в норме",
                "уклон ниже нормы - вода уходит медленно",
                "нулевой уклон - вода стоит",
                "направление течения",
                "водораздел - сток расходится",
                "точка сбора, выпуск есть",
                "точка сбора БЕЗ выпуска - ошибка",
                "сброс за пределы участка",
                "плато, направление не определено",
                "элемент сети, цвет = бассейн стока",
                "связь предположена модулем, не задана в проекте",
                "точка сброса бассейна"
            };

            double titleH = h * 1.35;
            double totalH = rowH * rows.Count + titleH * 2.6;

            // Рамка и подложка: легенда обязана читаться поверх любого плана.
            var frame = new[]
            {
                new Vector2D(origin.X - g, origin.Y + titleH * 1.8),
                new Vector2D(origin.X + width, origin.Y + titleH * 1.8),
                new Vector2D(origin.X + width, origin.Y + titleH * 1.8 - totalH),
                new Vector2D(origin.X - g, origin.Y + titleH * 1.8 - totalH)
            };
            GlyphBuilder.Fill(frame, RunoffStyle.Paper, emit);
            GlyphBuilder.Outline(frame, RunoffStyle.Muted, RunoffStyle.LineMain, true, emit);

            double unused;
            GlyphBuilder.Text("СХЕМА СТОКА", new Vector2D(origin.X, origin.Y + titleH * 0.4),
                              titleH, 0.0, RunoffStyle.Normal, emit, out unused);

            double yRow = origin.Y - titleH * 1.4;
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i](yRow);
                GlyphBuilder.Text(captions[i], new Vector2D(textX, yRow), h, 0.0,
                                  RunoffStyle.Muted, emit, out unused);
                yRow -= rowH;
            }
        }

        private static void LineRow(double x, double y, double g, Topomatic.Cad.Foundation.CadColor color,
                                    Topomatic.Dwg.Lineweight lw, Action<DwgEntity> emit)
        {
            GlyphBuilder.Line(new Vector2D(x - g * 1.2, y), new Vector2D(x + g * 1.2, y), color, lw, emit);
        }

        private static void WatershedSample(Vector2D p, double g, Action<DwgEntity> emit)
        {
            GlyphBuilder.Fill(new[]
            {
                new Vector2D(p.X, p.Y + g * 0.8),
                new Vector2D(p.X - g * 0.7, p.Y - g * 0.45),
                new Vector2D(p.X + g * 0.7, p.Y - g * 0.45)
            }, RunoffStyle.Normal, emit);
        }

        private static void CollectorSample(Vector2D p, double g, bool noOutlet, Action<DwgEntity> emit)
        {
            var color = noOutlet ? RunoffStyle.Error : RunoffStyle.Normal;
            GlyphBuilder.Disc(p, g * 0.5, color, emit);
            GlyphBuilder.Circle(p, g * 0.85, color,
                                noOutlet ? RunoffStyle.LineHeavy : RunoffStyle.LineThin, emit);
            if (noOutlet) GlyphBuilder.Circle(p, g * 1.15, RunoffStyle.Error, RunoffStyle.LineThin, emit);
        }

        private static void OutletSample(Vector2D p, double g, Action<DwgEntity> emit)
        {
            GlyphBuilder.Outline(new[]
            {
                new Vector2D(p.X, p.Y + g * 0.6),
                new Vector2D(p.X + g * 0.6, p.Y),
                new Vector2D(p.X, p.Y - g * 0.6),
                new Vector2D(p.X - g * 0.6, p.Y)
            }, RunoffStyle.Muted, RunoffStyle.LineMain, true, emit);
        }

        private static void StagnantSample(Vector2D p, double g, Action<DwgEntity> emit)
        {
            for (int i = -1; i <= 1; i += 2)
            {
                double x = p.X + g * 0.28 * i;
                GlyphBuilder.Line(new Vector2D(x, p.Y + g * 0.55), new Vector2D(x, p.Y - g * 0.55),
                                  RunoffStyle.Error, RunoffStyle.LineHeavy, emit);
            }
        }
    }
}
