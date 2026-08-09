using System;
using System.Collections.Generic;
using Topomatic.Cad.Foundation;
using Topomatic.Dwg;
using Topomatic.Dwg.Entities;

namespace AbrRunoff.Entity
{
    /// <summary>
    /// Низкоуровневые кирпичи отрисовки: залитые фигуры, линии, текст с подложкой.
    /// Вынесено из RunoffGeometry, чтобы построение схемы читалось как схема,
    /// а не как возня с BoundaryPath.
    /// </summary>
    internal static class GlyphBuilder
    {
        /// <summary>Залитый многоугольник. Заливка - только через DwgHatch SOLID.</summary>
        internal static void Fill(IList<Vector2D> pts, CadColor color, Action<DwgEntity> emit)
        {
            if (pts == null || pts.Count < 3) return;

            var hatch = new DwgHatch();
            hatch.PatternName = "SOLID";
            hatch.Color = color;

            var path = new PolylineBoundaryPath();
            foreach (var p in pts) path.Add(new BugleVector2D(p));
            path.IsClosed = true;
            hatch.BoundaryPath.Add(path);

            emit(hatch);
        }

        /// <summary>
        /// Полупрозрачная зона бассейна. Кладётся ПЕРВОЙ в блок - иначе накроет
        /// собственные линии схемы (грабли белых подложек под подписями).
        ///
        /// `DwgHatch.Transparency` принимает 0..255, где 0 = непрозрачно,
        /// 255 = полностью прозрачно (`Percentage` = обратная величина, проверено
        /// рефлексией SDK 16.0.62.12). Просят долю непрозрачности в процентах -
        /// так понятнее на месте вызова.
        /// </summary>
        internal static void Zone(IList<Vector2D> pts, CadColor color, double opacityPercent,
                                  Action<DwgEntity> emit)
        {
            if (pts == null || pts.Count < 3) return;

            var hatch = MakeHatch(pts, color);
            hatch.PatternName = "SOLID";
            hatch.Transparency = OpacityToTransparency(opacityPercent);
            emit(hatch);
        }

        /// <summary>
        /// Штрихованная зона бассейна - запасной язык, если прозрачность в чертеже
        /// не читается. Наклон свой у каждого бассейна, поэтому зоны различимы
        /// даже в монохромной печати.
        /// </summary>
        internal static void ZoneHatched(IList<Vector2D> pts, CadColor color, double angleDeg,
                                         double scale, Action<DwgEntity> emit)
        {
            if (pts == null || pts.Count < 3) return;

            var hatch = MakeHatch(pts, color);
            hatch.PatternName = "ANSI31";
            hatch.PatternAngle = angleDeg;
            hatch.PatternScale = scale <= 0.0 ? 1.0 : scale;
            emit(hatch);
        }

        private static DwgHatch MakeHatch(IList<Vector2D> pts, CadColor color)
        {
            var hatch = new DwgHatch();
            hatch.Color = color;

            var path = new PolylineBoundaryPath();
            foreach (var p in pts) path.Add(new BugleVector2D(p));
            path.IsClosed = true;
            hatch.BoundaryPath.Add(path);
            return hatch;
        }

        /// <summary>Доля непрозрачности в процентах -> Transparency (0 непрозр., 255 прозр.).</summary>
        private static Transparency OpacityToTransparency(double opacityPercent)
        {
            if (opacityPercent < 0.0) opacityPercent = 0.0;
            if (opacityPercent > 100.0) opacityPercent = 100.0;
            int value = (int)Math.Round(255.0 * (1.0 - opacityPercent / 100.0));
            if (value < 0) value = 0;
            if (value > 255) value = 255;
            return new Transparency(value);
        }

        /// <summary>Контур многоугольника.</summary>
        internal static void Outline(IList<Vector2D> pts, CadColor color, Lineweight lw,
                                     bool closed, Action<DwgEntity> emit)
        {
            if (pts == null || pts.Count < 2) return;

            var poly = new DwgPolyline();
            poly.Color = color;
            poly.Lineweight = lw;
            foreach (var p in pts) poly.Add(new BugleVector2D(p));
            poly.Closed = closed;
            emit(poly);
        }

        internal static void Line(Vector2D a, Vector2D b, CadColor color, Lineweight lw, Action<DwgEntity> emit)
        {
            var poly = new DwgPolyline();
            poly.Color = color;
            poly.Lineweight = lw;
            poly.Add(new BugleVector2D(a));
            poly.Add(new BugleVector2D(b));
            emit(poly);
        }

        internal static void Circle(Vector2D center, double radius, CadColor color,
                                    Lineweight lw, Action<DwgEntity> emit)
        {
            var c = new DwgCircle();
            c.Center = new Vector3D(center.X, center.Y, 0.0);
            c.Radius = radius;
            c.Color = color;
            c.Lineweight = lw;
            emit(c);
        }

        /// <summary>Залитый круг - через многоугольник, у DwgHatch нет круглого пути.</summary>
        internal static void Disc(Vector2D center, double radius, CadColor color, Action<DwgEntity> emit)
        {
            var pts = new List<Vector2D>(16);
            for (int i = 0; i < 16; i++)
            {
                double a = Math.PI * 2.0 * i / 16.0;
                pts.Add(new Vector2D(center.X + radius * Math.Cos(a), center.Y + radius * Math.Sin(a)));
            }
            Fill(pts, color, emit);
        }

        /// <summary>Залитый треугольник-наконечник остриём в tip по направлению dir.</summary>
        internal static void ArrowHead(Vector2D tip, Vector2D dir, double size, CadColor color,
                                       Action<DwgEntity> emit)
        {
            var back = new Vector2D(tip.X - dir.X * size, tip.Y - dir.Y * size);
            var norm = new Vector2D(-dir.Y, dir.X);
            double w = size * 0.42;

            Fill(new[]
            {
                tip,
                new Vector2D(back.X + norm.X * w, back.Y + norm.Y * w),
                new Vector2D(back.X - norm.X * w, back.Y - norm.Y * w)
            }, color, emit);
        }

        /// <summary>
        /// Текст подписи. Возвращает габарит занятого места - вызывающий разводит
        /// подписи по нему.
        ///
        /// Непрозрачной подложки под текстом НЕТ сознательно (убрана 2026-08-08).
        /// Подложка рисовалась как отдельный DwgHatch перед своим текстом, поэтому
        /// подложка следующей подписи ложилась ПОВЕРХ текста предыдущей - на плотном
        /// узле получалась россыпь белых прямоугольников без текста вообще. Порядок
        /// внутри блока эту гонку не лечит: подписи разных объектов схемы (по одному
        /// на дорогу) рисуются независимо и порядок между ними не определён.
        /// Читаемость теперь обеспечивают разведение (LabelLayout) и ручной оттаск
        /// подписи грипом.
        /// </summary>
        internal static void Text(string content, Vector2D anchor, double height, double rotationRad,
                                  CadColor color, Action<DwgEntity> emit, out double width)
        {
            width = EstimateWidth(content, height);

            var text = new DwgText();
            text.Content = content;
            text.Height = height;
            text.Color = color;
            text.Rotation = RunoffStyle.TextAngle(rotationRad);
            // Justify без TextAlignmentPoint игнорируется - точка выравнивания
            // это отдельное поле, Position при не-Left выравнивании не используется.
            text.Justify = TextAlignment.MiddleLeft;
            text.Position = new Vector3D(anchor.X, anchor.Y, 0.0);
            text.TextAlignmentPoint = new Vector3D(anchor.X, anchor.Y, 0.0);
            emit(text);
        }

        internal static double EstimateWidth(string content, double height)
        {
            return (content ?? "").Length * height * RunoffStyle.CharWidthRatio;
        }
    }
}
