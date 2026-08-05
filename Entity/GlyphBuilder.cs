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
        /// Текст с непрозрачной подложкой: без неё подпись тонет в линиях плана.
        /// Возвращает габарит занятого места - вызывающий разводит подписи по нему.
        /// </summary>
        internal static void Text(string content, Vector2D anchor, double height, double rotationRad,
                                  CadColor color, bool withBackground, Action<DwgEntity> emit,
                                  out double width)
        {
            width = EstimateWidth(content, height);

            if (withBackground)
            {
                double pad = height * 0.28;
                var box = BoxAt(anchor, width, height, pad, rotationRad);
                Fill(box, RunoffStyle.Paper, emit);
                Outline(box, RunoffStyle.Muted, RunoffStyle.LineThin, true, emit);
            }

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

        /// <summary>Прямоугольник подложки, повёрнутый вместе с текстом.</summary>
        private static Vector2D[] BoxAt(Vector2D anchor, double width, double height, double pad, double rot)
        {
            double cos = Math.Cos(rot), sin = Math.Sin(rot);
            double x0 = -pad, x1 = width + pad;
            double y0 = -height * 0.5 - pad, y1 = height * 0.5 + pad;

            return new[]
            {
                Local(anchor, x0, y0, cos, sin),
                Local(anchor, x1, y0, cos, sin),
                Local(anchor, x1, y1, cos, sin),
                Local(anchor, x0, y1, cos, sin)
            };
        }

        private static Vector2D Local(Vector2D origin, double dx, double dy, double cos, double sin)
        {
            return new Vector2D(origin.X + dx * cos - dy * sin, origin.Y + dx * sin + dy * cos);
        }
    }
}
