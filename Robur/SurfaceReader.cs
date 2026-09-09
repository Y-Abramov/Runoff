using System;
using AbrRunoff.Core.Watershed;
using Topomatic.Cad.Foundation;
using Topomatic.Sfc;

namespace AbrRunoff.Robur
{
    internal static class SurfaceReader
    {
        /// <summary>Шаг, предложенный по плотности треугольников и укладывающийся в потолок ячеек.</summary>
        internal static double SuggestStep(SurfaceRef s, WatershedSettings settings)
        {
            var b = s.Bounds;
            double w = b.Right - b.Left, h = b.Top - b.Bottom;
            double step = Rasterizer.SuggestStep(w * h, s.TriangleCount);
            return Rasterizer.FitStep(w, h, step, settings.MaxCells);
        }

        internal static Grid BuildGrid(SurfaceRef s, double step)
        {
            var surface = s.Surface;
            int pointCount = surface.Points.Count;

            var vx = new double[pointCount];
            var vy = new double[pointCount];
            var vz = new double[pointCount];

            for (int i = 0; i < pointCount; i++)
            {
                // Vector3D.X/Y/Z - ПОЛЯ, не свойства. Единая точка чтения - VectorRead:
                // рефлексия по свойствам молча вернёт нули (ловушка ловилась дважды).
                Vector3D v = surface.Points[i].Vertex;
                vx[i] = VectorRead.X(v);
                vy[i] = VectorRead.Y(v);
                vz[i] = VectorRead.Z(v);
            }

            int triCount = surface.Triangles.Count;
            var tri = new int[triCount * 3];
            int k = 0;
            for (int t = 0; t < triCount; t++)
            {
                SurfaceTriangle st = surface.Triangles[t];
                if (st.IsRemoved) continue;
                tri[k++] = st.A;
                tri[k++] = st.B;
                tri[k++] = st.C;
            }
            if (k < tri.Length) Array.Resize(ref tri, k);

            var b = s.Bounds;
            return Rasterizer.Build(vx, vy, vz, tri, b.Left, b.Bottom,
                                    b.Right - b.Left, b.Top - b.Bottom, step);
        }

        /// <summary>
        /// Врезка проектной поверхности: где проектный рельеф определён, его отметка
        /// перебивает естественную. Насыпь становится плотиной, и труба остаётся
        /// единственным выходом воды - иначе сток «переливается» через земляное
        /// полотно, которого в естественном рельефе нет.
        /// </summary>
        /// <summary>
        /// Хеш исходных данных водосбора: поверхность (имя + число треугольников +
        /// габарит), проектная поверхность, запрошенная точка створа, шаг. Смена
        /// любого - расчёт устарел. Строка, не число: сравнение строк дешевле
        /// повторного обхода TIN, и не нужно подбирать алгоритм хеширования.
        /// </summary>
        internal static string ComputeSourceHash(SurfaceRef surface, SurfaceRef design,
                                                 double outletX, double outletY, double step)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var b = surface.Bounds;
            return string.Format(ci, "{0}|{1}|{2:R},{3:R},{4:R},{5:R}|{6}|{7:R},{8:R}|{9:R}",
                surface.Name, surface.TriangleCount, b.Left, b.Bottom, b.Right, b.Top,
                design == null ? "" : design.Name, outletX, outletY, step);
        }

        internal static int BurnDesign(Grid g, SurfaceRef design)
        {
            int burned = 0;
            var surface = design.Surface;

            for (int i = 0; i < g.Count; i++)
            {
                var c = g.Center(i);
                double? z = surface.GetElevation(new Vector2D(c.X, c.Y));
                if (!z.HasValue) continue;
                g.Z[i] = z.Value;
                burned++;
            }
            return burned;
        }
    }
}
