using System;
using System.Collections.Generic;
using System.Globalization;
using AbrRunoff.Core;
using AbrRunoff.Core.Labels;
using AbrRunoff.Robur;
using Topomatic.Cad.Foundation;
using Topomatic.Dwg;
using Topomatic.Dwg.Entities;

namespace AbrRunoff.Entity
{
    /// <summary>
    /// Превращает RunoffResult в примитивы плана. Отдельно от примитива и
    /// контроллера, чтобы этим же кодом пользовался runoff_explode.
    ///
    /// Графический язык:
    ///   линия дна        - рвётся по участкам и красится статусом участка,
    ///                      так проблемное место видно ещё до чтения подписей;
    ///   стрелки          - залитые наконечники с хвостом, направление = ток воды;
    ///   водораздел       - треугольник вверх + две расходящиеся стрелки;
    ///   точка сбора      - залитый диск; без выпуска добавляется тревожное кольцо;
    ///   сброс с участка  - полый ромб;
    ///   подписи          - с непрозрачной подложкой и разведением, см. LabelPlacer.
    /// </summary>
    internal static class RunoffGeometry
    {
        /// <summary>Складывает всю графику схемы в переданный приёмник.</summary>
        /// <param name="offsets">Ручные смещения подписей (грипы); null - их нет.</param>
        /// <param name="placed">Приёмник размещённых подписей для грипов; null - не собирать.</param>
        internal static void Build(RunoffResult result, IList<DitchSample> left, IList<DitchSample> right,
                                   PlanProjector proj, RunoffSettings settings, bool detail,
                                   Action<DwgEntity> emit,
                                   LabelOffsets offsets = null, List<PlacedLabel> placed = null)
        {
            double glyph = RunoffStyle.GlyphBase * settings.GlyphScale;
            double textH = settings.TextHeight;

            // 1. Линия дна кювета - фоном, чтобы знаки и стрелки легли поверх.
            EmitDitchLines(result, left, proj, DitchSide.Left, emit);
            EmitDitchLines(result, right, proj, DitchSide.Right, emit);

            if (!detail)
            {
                // Дальний зум: только знаки экстремумов, без стрелок и текста.
                // Иначе на длинной трассе сотни глифов с подписями вешают отрисовку.
                foreach (var pt in result.Points)
                    EmitPointGlyph(pt, SamplesOf(pt.Side, left, right), proj, glyph * 1.4, emit);
                return;
            }

            // 2. Стрелки направления вдоль каждого участка.
            foreach (var seg in result.Segments)
            {
                if (seg.Direction == FlowDirection.None) { EmitStagnantMark(seg, SamplesOf(seg.Side, left, right), proj, glyph, emit); continue; }
                EmitArrows(seg, SamplesOf(seg.Side, left, right), proj, settings.ArrowStep, glyph, emit);
            }

            // 3. Знаки характерных точек - раньше подписей, чтобы подписи (и точечные,
            //    и уклона) обходили уже занятое место, а не наезжали на знаки.
            var placer = new LabelPlacer(offsets, placed);
            var anchors = new List<KeyValuePair<FlowPoint, Vector2D>>();

            foreach (var pt in result.Points)
            {
                Vector2D pos;
                if (!TryPointPos(pt, SamplesOf(pt.Side, left, right), proj, out pos)) continue;
                EmitPointGlyph(pt, SamplesOf(pt.Side, left, right), proj, glyph, emit);
                placer.Reserve(pos, glyph * 1.2);
                anchors.Add(new KeyValuePair<FlowPoint, Vector2D>(pt, pos));
            }

            // 4. Подписи характерных точек - в общий placer, приоритет выше подписей
            //    уклона (у них есть содержательный смысл "НЕТ ВЫПУСКА", их нельзя терять).
            if (settings.ShowPointLabels)
                foreach (var kv in anchors)
                {
                    var pt = kv.Key;
                    // Левый кювет подписываем выше линии, правый ниже - подписи двух
                    // сторон не сталкиваются даже на узкой трассе.
                    bool up = pt.Side == DitchSide.Left;
                    placer.Place(LabelKeys.Point(pt.Side, pt.Kind, pt.Station), LabelFor(pt), kv.Value,
                                 glyph * RunoffStyle.LabelGap, textH, ColorOfPoint(pt), up, emit);
                }

            // 5. Подписи уклона вдоль участка - повёрнуты по оси кювета, тот же
            //    placer: обходят и знаки, и уже поставленные подписи точек.
            if (settings.ShowGradeLabels)
                foreach (var seg in result.Segments)
                    EmitGradeLabel(seg, SamplesOf(seg.Side, left, right), proj, textH * 0.85, settings, placer, emit);
        }

        // ─────────────────────────────────────────────── линия дна по участкам

        /// <summary>
        /// Линия дна режется по участкам и красится статусом участка. Куски вне
        /// участков (одиночные отсчёты, хвосты) рисуются приглушённым цветом.
        /// </summary>
        private static void EmitDitchLines(RunoffResult result, IList<DitchSample> samples,
                                           PlanProjector proj, DitchSide side, Action<DwgEntity> emit)
        {
            var segs = new List<FlowSegment>();
            foreach (var s in result.Segments) if (s.Side == side) segs.Add(s);

            if (segs.Count == 0)
            {
                EmitPolyline(samples, proj, double.MinValue, double.MaxValue,
                             RunoffStyle.Muted, RunoffStyle.LineThin, emit);
                return;
            }

            foreach (var seg in segs)
            {
                EmitPolyline(samples, proj, seg.StationFrom, seg.StationTo,
                             ColorOfSegment(seg), WeightOfSegment(seg), emit);
            }
        }

        private static void EmitPolyline(IList<DitchSample> samples, PlanProjector proj,
                                         double from, double to, CadColor color, Lineweight lw,
                                         Action<DwgEntity> emit)
        {
            var pts = new List<Vector2D>();
            foreach (var s in samples)
            {
                if (!s.IsDitch)
                {
                    // Разрыв кювета: закрываем кусок, следующий начнётся заново.
                    if (pts.Count > 1) GlyphBuilder.Outline(pts, color, lw, false, emit);
                    pts.Clear();
                    continue;
                }
                if (s.Station < from - 1e-9 || s.Station > to + 1e-9) continue;

                Vector2D p;
                if (!proj.TryPoint(s.Station, s.Offset, out p)) continue;
                pts.Add(p);
            }
            if (pts.Count > 1) GlyphBuilder.Outline(pts, color, lw, false, emit);
        }

        // ─────────────────────────────────────────────── стрелки направления

        private static void EmitArrows(FlowSegment seg, IList<DitchSample> samples, PlanProjector proj,
                                       double step, double glyph, Action<DwgEntity> emit)
        {
            if (step < 1.0) step = 1.0;

            var color = ColorOfSegment(seg);
            // Короткий участок остался бы вообще без стрелки - ставим хотя бы одну
            // посередине, иначе направление на нём не прочитать.
            if (seg.Length < step)
            {
                EmitArrowAt(seg, samples, proj, (seg.StationFrom + seg.StationTo) * 0.5, glyph, color, emit);
                return;
            }

            for (double st = seg.StationFrom + step * 0.5; st < seg.StationTo; st += step)
                EmitArrowAt(seg, samples, proj, st, glyph, color, emit);
        }

        private static void EmitArrowAt(FlowSegment seg, IList<DitchSample> samples, PlanProjector proj,
                                        double station, double glyph, CadColor color, Action<DwgEntity> emit)
        {
            double offset = OffsetAt(samples, station);
            Vector2D pos, dir;
            if (!proj.TryPoint(station, offset, out pos)) return;
            if (!proj.TryTangent(station, out dir)) return;

            // Backward = вода идёт против роста пикетажа.
            if (seg.Direction == FlowDirection.Backward) dir = new Vector2D(-dir.X, -dir.Y);

            double head = glyph * 0.8;
            var tip = new Vector2D(pos.X + dir.X * head * 0.5, pos.Y + dir.Y * head * 0.5);
            var tail = new Vector2D(pos.X - dir.X * head * 1.1, pos.Y - dir.Y * head * 1.1);

            // Хвост: без него наконечник читается как случайный треугольник.
            GlyphBuilder.Line(tail, pos, color, RunoffStyle.LineMain, emit);
            GlyphBuilder.ArrowHead(tip, dir, head, color, emit);
        }

        /// <summary>Плато: направления нет, ставим знак стоячей воды - двойную поперечную черту.</summary>
        private static void EmitStagnantMark(FlowSegment seg, IList<DitchSample> samples, PlanProjector proj,
                                             double glyph, Action<DwgEntity> emit)
        {
            double station = (seg.StationFrom + seg.StationTo) * 0.5;
            double offset = OffsetAt(samples, station);
            Vector2D pos, dir;
            if (!proj.TryPoint(station, offset, out pos)) return;
            if (!proj.TryTangent(station, out dir)) return;

            var norm = new Vector2D(-dir.Y, dir.X);
            double h = glyph * 0.55;

            for (int i = -1; i <= 1; i += 2)
            {
                var c = new Vector2D(pos.X + dir.X * h * 0.35 * i, pos.Y + dir.Y * h * 0.35 * i);
                GlyphBuilder.Line(
                    new Vector2D(c.X + norm.X * h, c.Y + norm.Y * h),
                    new Vector2D(c.X - norm.X * h, c.Y - norm.Y * h),
                    RunoffStyle.Error, RunoffStyle.LineHeavy, emit);
            }
        }

        // ─────────────────────────────────────────────── подпись уклона

        private static void EmitGradeLabel(FlowSegment seg, IList<DitchSample> samples, PlanProjector proj,
                                           double height, RunoffSettings settings, LabelPlacer placer,
                                           Action<DwgEntity> emit)
        {
            // На коротком участке подпись длиннее самого участка - только мешает.
            if (seg.Length < height * 12.0) return;

            double station = (seg.StationFrom + seg.StationTo) * 0.5;
            double offset = OffsetAt(samples, station);
            Vector2D pos, dir;
            if (!proj.TryPoint(station, offset, out pos)) return;
            if (!proj.TryTangent(station, out dir)) return;

            string content = string.Format(CultureInfo.InvariantCulture, "{0:F1} ‰", seg.AvgGradePermille);
            double rot = Math.Atan2(dir.Y, dir.X);

            // Текст вверх ногами не читают: разворачиваем на 180 и подписываем
            // с другого конца, смысл не меняется.
            if (rot > Math.PI * 0.5 || rot < -Math.PI * 0.5) rot += Math.PI;

            // Сдвигаем подпись с самой линии наружу, чтобы не легла на стрелки.
            var norm = new Vector2D(-dir.Y, dir.X);
            double side = seg.Side == DitchSide.Left ? 1.0 : -1.0;
            var anchor = new Vector2D(pos.X + norm.X * height * 1.6 * side,
                                      pos.Y + norm.Y * height * 1.6 * side);

            // gap=0 - anchor уже отодвинут от линии дна через norm выше; placer
            // при столкновении с уже занятым местом (знаки, подписи точек, другие
            // подписи уклона) доищет свободный слот дальше по вертикали.
            placer.Place(LabelKeys.Grade(seg.Side, seg.StationFrom, seg.StationTo), content, anchor,
                         0.0, height, ColorOfSegment(seg), seg.Side == DitchSide.Left, emit, rot);
        }

        // ─────────────────────────────────────────────── знаки характерных точек

        private static bool TryPointPos(FlowPoint pt, IList<DitchSample> samples, PlanProjector proj,
                                        out Vector2D pos)
        {
            return proj.TryPoint(pt.Station, OffsetAt(samples, pt.Station), out pos);
        }

        private static void EmitPointGlyph(FlowPoint pt, IList<DitchSample> samples, PlanProjector proj,
                                           double glyph, Action<DwgEntity> emit)
        {
            Vector2D pos;
            if (!TryPointPos(pt, samples, proj, out pos)) return;

            var color = ColorOfPoint(pt);
            Vector2D dir;
            if (!proj.TryTangent(pt.Station, out dir)) dir = new Vector2D(1.0, 0.0);

            switch (pt.Kind)
            {
                case PointKind.Watershed:
                    EmitWatershed(pos, dir, glyph, color, emit);
                    break;
                case PointKind.Collector:
                    EmitCollector(pt, pos, glyph, color, emit);
                    break;
                default:
                    EmitOutlet(pos, dir, glyph, color, emit);
                    break;
            }
        }

        /// <summary>Водораздел: залитый треугольник вверх + две расходящиеся стрелки.</summary>
        private static void EmitWatershed(Vector2D pos, Vector2D dir, double g, CadColor color,
                                          Action<DwgEntity> emit)
        {
            GlyphBuilder.Fill(new[]
            {
                new Vector2D(pos.X, pos.Y + g),
                new Vector2D(pos.X - g * 0.85, pos.Y - g * 0.55),
                new Vector2D(pos.X + g * 0.85, pos.Y - g * 0.55)
            }, color, emit);

            // Расхождение потока читается мгновенно, без чтения подписи.
            double reach = g * 2.0;
            for (int i = -1; i <= 1; i += 2)
            {
                var d = new Vector2D(dir.X * i, dir.Y * i);
                var tip = new Vector2D(pos.X + d.X * reach, pos.Y + d.Y * reach);
                GlyphBuilder.Line(pos, tip, color, RunoffStyle.LineThin, emit);
                GlyphBuilder.ArrowHead(tip, d, g * 0.55, color, emit);
            }
        }

        /// <summary>Точка сбора: залитый диск. Без выпуска - тревожное кольцо вокруг.</summary>
        private static void EmitCollector(FlowPoint pt, Vector2D pos, double g, CadColor color,
                                          Action<DwgEntity> emit)
        {
            GlyphBuilder.Disc(pos, g * 0.62, color, emit);

            if (pt.Problem == ProblemKind.NoOutlet)
            {
                // Двойное кольцо - самая дорогая ошибка проекта, её нельзя пролистать.
                GlyphBuilder.Circle(pos, g * 1.05, RunoffStyle.Error, RunoffStyle.LineHeavy, emit);
                GlyphBuilder.Circle(pos, g * 1.45, RunoffStyle.Error, RunoffStyle.LineThin, emit);
            }
            else
            {
                // Труба есть: спокойное кольцо тем же цветом.
                GlyphBuilder.Circle(pos, g * 1.05, color, RunoffStyle.LineThin, emit);
            }
        }

        /// <summary>Сброс за пределы участка: полый ромб со стрелкой наружу.</summary>
        private static void EmitOutlet(Vector2D pos, Vector2D dir, double g, CadColor color,
                                       Action<DwgEntity> emit)
        {
            GlyphBuilder.Outline(new[]
            {
                new Vector2D(pos.X, pos.Y + g * 0.7),
                new Vector2D(pos.X + g * 0.7, pos.Y),
                new Vector2D(pos.X, pos.Y - g * 0.7),
                new Vector2D(pos.X - g * 0.7, pos.Y)
            }, color, RunoffStyle.LineMain, true, emit);
        }

        // ─────────────────────────────────────────────── цвета и подписи

        private static CadColor ColorOfSegment(FlowSegment seg)
        {
            // Нулевой уклон тяжелее «ниже нормы»: там вода не течёт вообще,
            // а не просто медленно. Отсюда красный против янтарного.
            if ((seg.Flags & SegmentFlags.ZeroGrade) != 0) return RunoffStyle.Error;
            if ((seg.Flags & SegmentFlags.BelowNorm) != 0) return RunoffStyle.Warning;
            return RunoffStyle.Normal;
        }

        private static Lineweight WeightOfSegment(FlowSegment seg)
        {
            bool bad = (seg.Flags & (SegmentFlags.ZeroGrade | SegmentFlags.BelowNorm)) != 0;
            return bad ? RunoffStyle.LineHeavy : RunoffStyle.LineMain;
        }

        private static CadColor ColorOfPoint(FlowPoint pt)
        {
            if (pt.Problem == ProblemKind.NoOutlet) return RunoffStyle.Error;
            if (pt.Kind == PointKind.Outlet) return RunoffStyle.Muted;
            return RunoffStyle.Normal;
        }

        private static string LabelFor(FlowPoint pt)
        {
            var ci = CultureInfo.InvariantCulture;
            string head = pt.Kind == PointKind.Watershed ? "водораздел"
                        : pt.Kind == PointKind.Collector ? "сбор" : "сброс";
            string body = string.Format(ci, "{0} ПК{1:F0}  {2:F2}", head, pt.Station, pt.BottomZ);

            if (pt.PipeStation.HasValue)
            {
                body += pt.PipeDiameter.HasValue && pt.PipeDiameter.Value > 0.0
                    ? string.Format(ci, "  труба ПК{0:F0} d{1:F2}", pt.PipeStation.Value, pt.PipeDiameter.Value)
                    : string.Format(ci, "  труба ПК{0:F0}", pt.PipeStation.Value);
            }
            else if (pt.Problem == ProblemKind.NoOutlet)
            {
                body += "  НЕТ ВЫПУСКА";
            }
            return body;
        }

        private static IList<DitchSample> SamplesOf(DitchSide side, IList<DitchSample> left, IList<DitchSample> right)
        {
            return side == DitchSide.Left ? left : right;
        }

        private static double OffsetAt(IList<DitchSample> samples, double station)
        {
            double best = 0.0, bestDist = double.MaxValue;
            foreach (var s in samples)
            {
                if (!s.IsDitch) continue;
                double d = Math.Abs(s.Station - station);
                if (d >= bestDist) continue;
                bestDist = d; best = s.Offset;
            }
            return best;
        }
    }
}
