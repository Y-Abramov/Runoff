using System;
using System.Collections.Generic;

namespace AbrRunoff.Core.Watershed
{
    /// <summary>
    /// Упрощение контура и его площадь. Площадь считается по УПРОЩЁННОМУ полигону,
    /// а не по числу ячеек: цифра в ведомости обязана совпадать с тем, что
    /// проверяющий обмерит на чертеже.
    /// </summary>
    public static class Simplify
    {
        public static List<Pt> DouglasPeucker(List<Pt> pts, double tolerance)
        {
            if (pts.Count < 3) return new List<Pt>(pts);

            var keep = new bool[pts.Count];
            keep[0] = true;
            keep[pts.Count - 1] = true;

            var stack = new Stack<int[]>();
            stack.Push(new[] { 0, pts.Count - 1 });

            while (stack.Count > 0)
            {
                var seg = stack.Pop();
                int first = seg[0], last = seg[1];
                double maxDist = -1.0;
                int index = -1;

                for (int i = first + 1; i < last; i++)
                {
                    double d = PerpendicularDistance(pts[i], pts[first], pts[last]);
                    if (d > maxDist) { maxDist = d; index = i; }
                }

                if (maxDist > tolerance && index > 0)
                {
                    keep[index] = true;
                    stack.Push(new[] { first, index });
                    stack.Push(new[] { index, last });
                }
            }

            var result = new List<Pt>();
            for (int i = 0; i < pts.Count; i++) if (keep[i]) result.Add(pts[i]);
            return result;
        }

        private static double PerpendicularDistance(Pt p, Pt a, Pt b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len2 = dx * dx + dy * dy;
            if (len2 < 1e-18) return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
            double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2;
            if (t < 0.0) t = 0.0; else if (t > 1.0) t = 1.0;
            double px = a.X + t * dx, py = a.Y + t * dy;
            return Math.Sqrt((p.X - px) * (p.X - px) + (p.Y - py) * (p.Y - py));
        }

        /// <summary>Площадь кольца со знаком (шнурование). Знак говорит о направлении обхода.</summary>
        public static double RingArea(List<Pt> ring)
        {
            double s = 0.0;
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
                s += (ring[j].X * ring[i].Y) - (ring[i].X * ring[j].Y);
            return s * 0.5;
        }

        /// <summary>Первое кольцо - внешнее, остальные вычитаются как дырки.</summary>
        public static double PolygonArea(List<List<Pt>> rings)
        {
            if (rings.Count == 0) return 0.0;
            double area = Math.Abs(RingArea(rings[0]));
            for (int i = 1; i < rings.Count; i++) area -= Math.Abs(RingArea(rings[i]));
            return area;
        }

        /// <summary>
        /// Подбирает допуск упрощения так, чтобы площадь не убежала дальше
        /// заданного процента от площади маски. Начинаем с крупного допуска и
        /// уменьшаем вдвое, пока не уложимся.
        /// </summary>
        public static List<List<Pt>> FitTolerance(List<List<Pt>> rings, double maskArea,
                                                  double step, double startCells, double tolerancePercent)
        {
            double tol = step * startCells;
            List<List<Pt>> best = null;

            for (int attempt = 0; attempt < 12; attempt++)
            {
                var simplified = new List<List<Pt>>();
                foreach (var r in rings) simplified.Add(DouglasPeucker(r, tol));
                best = simplified;

                double area = PolygonArea(simplified);
                double diff = maskArea > 0.0 ? Math.Abs(area - maskArea) / maskArea * 100.0 : 0.0;
                if (diff <= tolerancePercent) return simplified;

                tol *= 0.5;
            }
            return best;
        }
    }
}
