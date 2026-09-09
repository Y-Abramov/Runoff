using System;

namespace AbrRunoff.Core.Watershed
{
    /// <summary>
    /// Треугольники TIN -> регулярная сетка. Идём по треугольникам, а не по ячейкам:
    /// точечный поиск отметки на каждую ячейку - это десятки тысяч независимых
    /// поисков по дереву, а обход треугольников делает ту же работу за один проход.
    /// </summary>
    public static class Rasterizer
    {
        private static readonly double[] s_Ladder = { 0.5, 1.0, 2.0, 5.0, 10.0, 20.0, 50.0, 100.0 };

        /// <summary>
        /// Шаг по плотности исходных данных: мельче треугольника дробить бессмысленно.
        /// Классическое округление 1-2-5 по десятичному порядку (D3 tickStep, Wilkinson):
        /// линейный проход по s_Ladder с "raw &lt;= rung" даёт неверный результат на
        /// границах декад (sqrt(200) точно равен границе между 10 и 20), решает
        /// сравнение доли числа внутри своего порядка с порогами 1.5/3/7.
        /// </summary>
        public static double SuggestStep(double areaM2, int triangleCount)
        {
            if (triangleCount <= 0 || areaM2 <= 0.0) return 5.0;
            double raw = Math.Sqrt(areaM2 / triangleCount);
            if (raw <= 0.0) return 5.0;

            double magnitude = Math.Pow(10.0, Math.Floor(Math.Log10(raw)));
            double fraction = raw / magnitude;
            double nice = fraction <= 1.5 ? 1.0 : (fraction <= 3.0 ? 2.0 : (fraction <= 7.0 ? 5.0 : 10.0));
            double step = nice * magnitude;

            if (step < s_Ladder[0]) return s_Ladder[0];
            if (step > s_Ladder[s_Ladder.Length - 1]) return s_Ladder[s_Ladder.Length - 1];
            return step;
        }

        /// <summary>Поднимает шаг по лестнице, пока сетка не уложится в потолок ячеек.</summary>
        public static double FitStep(double width, double height, double step, int maxCells)
        {
            double s = step;
            while (Cells(width, height, s) > maxCells)
            {
                double next = 0.0;
                for (int i = 0; i < s_Ladder.Length; i++)
                    if (s_Ladder[i] > s) { next = s_Ladder[i]; break; }
                if (next <= 0.0) { s *= 2.0; } else { s = next; }
            }
            return s;
        }

        private static double Cells(double width, double height, double step)
        {
            double nx = Math.Floor(width / step) + 1.0;
            double ny = Math.Floor(height / step) + 1.0;
            return nx * ny;
        }

        /// <param name="tri">Индексы вершин по три на треугольник.</param>
        public static Grid Build(double[] vx, double[] vy, double[] vz, int[] tri,
                                 double minX, double minY, double width, double height, double step)
        {
            int nx = (int)Math.Floor(width / step) + 1;
            int ny = (int)Math.Floor(height / step) + 1;
            var g = new Grid(minX, minY, step, nx, ny);

            for (int t = 0; t + 2 < tri.Length; t += 3)
            {
                int a = tri[t], b = tri[t + 1], c = tri[t + 2];
                double ax = vx[a], ay = vy[a], az = vz[a];
                double bx = vx[b], by = vy[b], bz = vz[b];
                double cx = vx[c], cy = vy[c], cz = vz[c];

                double d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy);
                if (Math.Abs(d) < 1e-12) continue;   // вырожденный треугольник

                int ix0 = ClampIx(g, Math.Min(ax, Math.Min(bx, cx)));
                int ix1 = ClampIx(g, Math.Max(ax, Math.Max(bx, cx)));
                int iy0 = ClampIy(g, Math.Min(ay, Math.Min(by, cy)));
                int iy1 = ClampIy(g, Math.Max(ay, Math.Max(by, cy)));

                for (int iy = iy0; iy <= iy1; iy++)
                {
                    double py = g.CellY(iy);
                    for (int ix = ix0; ix <= ix1; ix++)
                    {
                        double px = g.CellX(ix);

                        double l1 = ((by - cy) * (px - cx) + (cx - bx) * (py - cy)) / d;
                        double l2 = ((cy - ay) * (px - cx) + (ax - cx) * (py - cy)) / d;
                        double l3 = 1.0 - l1 - l2;
                        const double eps = -1e-9;
                        if (l1 < eps || l2 < eps || l3 < eps) continue;

                        g.Z[g.Index(ix, iy)] = l1 * az + l2 * bz + l3 * cz;
                    }
                }
            }
            return g;
        }

        private static int ClampIx(Grid g, double x)
        {
            int i = (int)Math.Round((x - g.OriginX) / g.Step);
            return i < 0 ? 0 : (i > g.Nx - 1 ? g.Nx - 1 : i);
        }

        private static int ClampIy(Grid g, double y)
        {
            int i = (int)Math.Round((y - g.OriginY) / g.Step);
            return i < 0 ? 0 : (i > g.Ny - 1 ? g.Ny - 1 : i);
        }
    }
}
