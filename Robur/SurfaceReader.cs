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
