using System;
using System.Collections.Generic;
using System.Globalization;
using AbrRunoff.Core;
using AbrRunoff.Core.Labels;
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
        /// <summary>
        /// ПОРЯДОК НАЛОЖЕНИЯ ЗДЕСЬ ЗНАЧИМ и менять его нельзя без причины:
        ///   1) зоны бассейнов (заливка)  - фон, иначе накроют собственные линии;
        ///   2) линии элементов и рёбра;
        ///   3) знаки узлов;
        ///   4) подписи.
        /// Раньше порядок складывался как получится - отсюда часть «слипания».
        /// </summary>
        internal static void Build(DrainageNetwork net, Dictionary<int, int> basins,
                                   double glyph, double textH, bool detail,
                                   Action<DwgEntity> emit,
                                   LabelOffsets offsets = null, List<PlacedLabel> placed = null,
                                   RunoffSettings settings = null)
        {
            var byId = new Dictionary<int, DrainageNode>(net.Nodes.Count);
            foreach (var n in net.Nodes) byId[n.Id] = n;

            // 0. Зоны бассейнов - фоном под всей схемой.
            var summary = BasinSummary.Build(net, basins);
            EmitBasinZones(summary, settings ?? new RunoffSettings(), glyph, emit);

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

                if (e.Element == ElementKind.Pipe)
                    EmitPipe(pa, pb, color, glyph, emit);
                else if (e.Confidence == LinkConfidence.Inferred)
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
                                      textH, 0.0, RunoffStyle.Warning, emit, out unused);
                }
            }

            // 2. Узлы выпуска - крупно, они и есть ответ на вопрос «куда стекает».
            var placer = new LabelPlacer(offsets, placed);
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
                placer.Place(LabelKeys.Node(n.X, n.Y), OutfallLabel(n, basin), pos,
                             glyph * RunoffStyle.LabelGap, textH, color, true, emit);
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
                placer.Place(LabelKeys.Node(n.X, n.Y), "сток не доходит до выпуска", pos,
                             glyph * RunoffStyle.LabelGap, textH, RunoffStyle.Error, false, emit);
            }
        }

        /// <summary>
        /// Зона бассейна: полупрозрачная заливка (или штриховка) плюс контур той же
        /// краской пунктиром. Контур и есть граница между бассейнами - её видно как
        /// зону, а не как одинокий треугольник водораздела в точке.
        ///
        /// Бассейн без выпуска (Index = -1) обводится КРАСНЫМ и жирно: цвет здесь
        /// уже не про принадлежность, а про беду - это единственное место, где
        /// красный вмешивается в язык зон.
        /// </summary>
        private static void EmitBasinZones(List<BasinInfo> summary, RunoffSettings settings,
                                           double glyph, Action<DwgEntity> emit)
        {
            if (settings.BasinZone == BasinZoneStyle.None) return;

            double half = Math.Max(glyph * settings.BasinBandWidth * 0.5, 0.1);

            foreach (var basin in summary)
            {
                if (basin.Segments == null || basin.Segments.Count == 0) continue;
                var color = BasinPalette.For(basin.Index);

                // Лента звеньями: каждое ребро получает свою полосу. Полосы
                // соседних звеньев перекрываются на изломе - для подсветки это
                // незаметно и дешевле честного объединения контуров.
                foreach (var s in basin.Segments)
                {
                    var band = Band(s, half);
                    if (band == null) continue;

                    if (settings.BasinZone == BasinZoneStyle.Hatched)
                        GlyphBuilder.ZoneHatched(band, color, BasinPalette.HatchAngle(basin.Index),
                                                 Math.Max(glyph, 1.0), emit);
                    else
                        GlyphBuilder.Zone(band, color, settings.BasinZoneOpacity, emit);
                }
            }
        }

        /// <summary>
        /// Труба: двойная линия по длине плюс поперечные засечки-оголовки на концах.
        /// Начертание нарочно НЕ такое, как у кювета - это искусственное сооружение,
        /// и на схеме оно обязано читаться иначе, чем канава.
        /// </summary>
        private static void EmitPipe(Vector2D a, Vector2D b, CadColor color, double glyph,
                                     Action<DwgEntity> emit)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9) return;

            double ux = dx / len, uy = dy / len;
            double half = glyph * 0.22;                  // полуширина «стенок»
            double nx = -uy * half, ny = ux * half;

            GlyphBuilder.Line(new Vector2D(a.X + nx, a.Y + ny), new Vector2D(b.X + nx, b.Y + ny),
                              color, RunoffStyle.LineMain, emit);
            GlyphBuilder.Line(new Vector2D(a.X - nx, a.Y - ny), new Vector2D(b.X - nx, b.Y - ny),
                              color, RunoffStyle.LineMain, emit);

            // Оголовки - поперечные засечки шире самой трубы.
            double hx = -uy * glyph * 0.45, hy = ux * glyph * 0.45;
            GlyphBuilder.Line(new Vector2D(a.X + hx, a.Y + hy), new Vector2D(a.X - hx, a.Y - hy),
                              color, RunoffStyle.LineHeavy, emit);
            GlyphBuilder.Line(new Vector2D(b.X + hx, b.Y + hy), new Vector2D(b.X - hx, b.Y - hy),
                              color, RunoffStyle.LineHeavy, emit);
        }

        /// <summary>Полоса вдоль звена сети - прямоугольник шириной 2*half.</summary>
        private static List<Vector2D> Band(BasinSegment s, double half)
        {
            double dx = s.X2 - s.X1, dy = s.Y2 - s.Y1;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9) return null;

            double nx = -dy / len * half, ny = dx / len * half;
            return new List<Vector2D>
            {
                new Vector2D(s.X1 + nx, s.Y1 + ny),
                new Vector2D(s.X2 + nx, s.Y2 + ny),
                new Vector2D(s.X2 - nx, s.Y2 - ny),
                new Vector2D(s.X1 - nx, s.Y1 - ny)
            };
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
            EmitDashedWeighted(a, b, color, glyph * 0.8, RunoffStyle.LineMain, emit);
        }

        private static void EmitDashedWeighted(Vector2D a, Vector2D b, CadColor color, double dashLen,
                                               Lineweight weight, Action<DwgEntity> emit)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9) return;

            double dash = Math.Max(dashLen, len / 40.0);
            var dir = new Vector2D(dx / len, dy / len);

            for (double s = 0.0; s < len; s += dash * 2.0)
            {
                double e = Math.Min(s + dash, len);
                GlyphBuilder.Line(
                    new Vector2D(a.X + dir.X * s, a.Y + dir.Y * s),
                    new Vector2D(a.X + dir.X * e, a.Y + dir.Y * e),
                    color, weight, emit);
            }
        }
    }
}
