using System;
using System.Collections.Generic;
using System.Globalization;
using AbrRunoff.Core.Network;
using Topomatic.Cad.Foundation;
using Topomatic.Dwg;
using Topomatic.Dwg.Entities;

namespace AbrRunoff.Entity
{
    /// <summary>
    /// Графика сети водоотвода. Знаки, стрелки и подписи переиспользуются из v1
    /// (GlyphBuilder, LabelPlacer) - схема одной дороги и сеть должны выглядеть
    /// как один инструмент, а не как два разных.
    /// </summary>
    internal static class NetworkGeometry
    {
        internal static void Build(DrainageNetwork net, Dictionary<int, int> basins,
                                   double glyph, double textH, bool detail,
                                   Action<DwgEntity> emit)
        {
            var byId = new Dictionary<int, DrainageNode>(net.Nodes.Count);
            foreach (var n in net.Nodes) byId[n.Id] = n;

            // 1. Рёбра: цвет по бассейну, начертание по достоверности.
            foreach (var e in net.Edges)
            {
                DrainageNode a, b;
                if (!byId.TryGetValue(e.FromNode, out a)) continue;
                if (!byId.TryGetValue(e.ToNode, out b)) continue;

                int basin;
                if (!basins.TryGetValue(e.FromNode, out basin)) basin = -1;
                var color = BasinPalette.For(basin);

                var pa = new Vector2D(a.X, a.Y);
                var pb = new Vector2D(b.X, b.Y);

                if (e.Confidence == LinkConfidence.Inferred)
                    EmitDashed(pa, pb, color, glyph, emit);
                else
                    GlyphBuilder.Line(pa, pb, color, RunoffStyle.LineMain, emit);

                if (!detail) continue;

                // Стрелка в середине ребра - направление течения.
                double dx = pb.X - pa.X, dy = pb.Y - pa.Y;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len < 1e-6) continue;

                var dir = new Vector2D(dx / len, dy / len);
                var mid = new Vector2D((pa.X + pb.X) * 0.5, (pa.Y + pb.Y) * 0.5);
                GlyphBuilder.ArrowHead(mid, dir, glyph * 0.7, color, emit);

                // Догадка помечается вопросом - её видно глазом, без чтения ведомости.
                if (e.Confidence == LinkConfidence.Inferred)
                {
                    double unused;
                    GlyphBuilder.Text("?", new Vector2D(mid.X + glyph * 0.6, mid.Y + glyph * 0.6),
                                      textH, 0.0, RunoffStyle.Warning, false, emit, out unused);
                }
            }

            // 2. Узлы выпуска - крупно, они и есть ответ на вопрос «куда стекает».
            var placer = new LabelPlacer();
            foreach (var n in net.Nodes)
            {
                if (n.Outfall == OutfallKind.None) continue;

                int basin;
                if (!basins.TryGetValue(n.Id, out basin)) basin = -1;
                var color = BasinPalette.For(basin);
                var pos = new Vector2D(n.X, n.Y);

                GlyphBuilder.Disc(pos, glyph * 0.7, color, emit);
                GlyphBuilder.Circle(pos, glyph * 1.2, color, RunoffStyle.LineHeavy, emit);
                placer.Reserve(pos, glyph * 1.4);

                if (!detail) continue;
                placer.Place(OutfallLabel(n, basin), pos, glyph * RunoffStyle.LabelGap,
                             textH, color, true, true, emit);
            }

            // 3. Тупики - красным: сток никуда не приходит.
            if (!detail) return;
            foreach (var n in net.Nodes)
            {
                if (n.Outfall != OutfallKind.None) continue;
                int basin;
                if (basins.TryGetValue(n.Id, out basin) && basin >= 0) continue;
                if (!IsChainEnd(net, n.Id)) continue;

                var pos = new Vector2D(n.X, n.Y);
                GlyphBuilder.Circle(pos, glyph * 1.05, RunoffStyle.Error, RunoffStyle.LineHeavy, emit);
                GlyphBuilder.Circle(pos, glyph * 1.45, RunoffStyle.Error, RunoffStyle.LineThin, emit);
                placer.Place("сток не доходит до выпуска", pos, glyph * RunoffStyle.LabelGap,
                             textH, RunoffStyle.Error, true, false, emit);
            }
        }

        /// <summary>Узел без исходящих рёбер - конец цепочки.</summary>
        private static bool IsChainEnd(DrainageNetwork net, int nodeId)
        {
            foreach (var e in net.Edges) if (e.FromNode == nodeId) return false;
            return true;
        }

        private static string OutfallLabel(DrainageNode n, int basin)
        {
            string kind = n.Outfall == OutfallKind.StormWell ? "колодец"
                        : n.Outfall == OutfallKind.PipeOutward ? "труба"
                        : "сброс";
            return string.Format(CultureInfo.InvariantCulture,
                "бассейн {0}: {1}  {2:F2}", basin + 1, kind, n.Z);
        }

        /// <summary>
        /// Пунктир вручную: тип линии Robur пришлось бы регистрировать в чертеже,
        /// а рисованный штрих одинаково выглядит и в схеме, и после взрыва.
        /// </summary>
        private static void EmitDashed(Vector2D a, Vector2D b, CadColor color, double glyph,
                                       Action<DwgEntity> emit)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9) return;

            double dash = Math.Max(glyph * 0.8, len / 40.0);
            var dir = new Vector2D(dx / len, dy / len);

            for (double s = 0.0; s < len; s += dash * 2.0)
            {
                double e = Math.Min(s + dash, len);
                GlyphBuilder.Line(
                    new Vector2D(a.X + dir.X * s, a.Y + dir.Y * s),
                    new Vector2D(a.X + dir.X * e, a.Y + dir.Y * e),
                    color, RunoffStyle.LineMain, emit);
            }
        }
    }
}
