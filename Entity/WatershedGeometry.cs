using System;
using System.Collections.Generic;
using AbrRunoff.Core.Watershed;
using Topomatic.Cad.Foundation;
using Topomatic.Dwg;
using Topomatic.Dwg.Entities;

namespace AbrRunoff.Entity
{
    internal static class WatershedGeometry
    {
        /// <summary>Доля непрозрачности заливки бассейна, %. Тот же порядок, что у зон бассейнов сети (18%).</summary>
        private const double ZoneOpacityPercent = 15.0;

        /// <summary>
        /// Контур + лог + знаки. Зона бассейна - лёгкая полупрозрачная заливка
        /// (решение юзера 2026-09-09: со штриховкой бассейн читался плохо).
        /// Прозрачность низкая специально: площадь в квадратные километры,
        /// закрашенная плотно, забивает подложку плана.
        ///
        /// Заливается только внешнее кольцо: дырки маски (островки NoData внутри
        /// водосбора) заливка накроет - редкий случай, известное упрощение.
        ///
        /// Контур целиком красится цветом ошибки при hasProblem: точных границ
        /// проблемного УЧАСТКА контура ядро не отдаёт (WatershedMask.TouchesEdge -
        /// признак на всю маску, не по рёбрам) - известное упрощение, честнее
        /// подсветить весь контур, чем выдумывать несуществующую точность.
        /// </summary>
        internal static void Build(WatershedResult r, double glyphScale, double textHeight,
                                   bool detailed, bool hasProblem, Action<DwgEntity> emit)
        {
            CadColor contourColor = hasProblem ? RunoffStyle.Error : RunoffStyle.Normal;

            for (int k = 0; k < r.Contour.Count; k++)
            {
                var pts = ToVectors(r.Contour[k]);
                if (k == 0) GlyphBuilder.Zone(pts, contourColor, ZoneOpacityPercent, emit);
                GlyphBuilder.Outline(pts, contourColor, RunoffStyle.LineMain, true, emit);
            }

            var path = ToVectors(r.Path);
            GlyphBuilder.Outline(path, RunoffStyle.Normal, RunoffStyle.LineHeavy, false, emit);

            if (!detailed || path.Count < 2) return;

            Vector2D tip = path[path.Count - 1];
            Vector2D prev = path[path.Count - 2];
            double dx = tip.X - prev.X, dy = tip.Y - prev.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len > 1e-9)
                GlyphBuilder.ArrowHead(tip, new Vector2D(dx / len, dy / len), glyphScale, RunoffStyle.Normal, emit);

            GlyphBuilder.Circle(path[0], glyphScale * 0.5, RunoffStyle.Normal, RunoffStyle.LineThin, emit);
            GlyphBuilder.Disc(tip, glyphScale * 0.4, RunoffStyle.Normal, emit);
        }

        private static List<Vector2D> ToVectors(IList<Pt> pts)
        {
            var list = new List<Vector2D>(pts.Count);
            foreach (var p in pts) list.Add(new Vector2D(p.X, p.Y));
            return list;
        }

        internal static string SummaryText(WatershedResult r)
        {
            return "F = " + r.AreaKm2.ToString("0.###") + " км2   L = " +
                   (r.LengthM / 1000.0).ToString("0.###") + " км   i = " +
                   r.EndSlope.ToString("0.####");
        }
    }
}
