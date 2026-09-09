using System;
using System.Collections.Generic;

namespace AbrRunoff.Core.Watershed
{
    public static class Metrics
    {
        /// <summary>Плановая длина ломаной по ячейкам.</summary>
        public static double PathLength(Grid g, IList<int> path)
        {
            double sum = 0.0;
            for (int k = 1; k < path.Count; k++)
            {
                var a = g.Center(path[k - 1]);
                var b = g.Center(path[k]);
                double dx = b.X - a.X, dy = b.Y - a.Y;
                sum += Math.Sqrt(dx * dx + dy * dy);
            }
            return sum;
        }

        /// <summary>Уклон по концам лога - то, чем пользуется проектировщик в простом расчёте.</summary>
        public static double EndSlope(double headZ, double outletZ, double length)
        {
            if (length <= 0.0) return 0.0;
            return (headZ - outletZ) / length;
        }

        /// <summary>
        /// Средневзвешенный уклон ломаного профиля:
        /// I = ((h0+h1)l1 + (h1+h2)l2 + ... + (h(n-1)+hn)ln - 2*h0*L) / L^2,
        /// где h0 - отметка створа, h_i - отметки точек перелома вверх по логу,
        /// l_i - длины участков, L - полная длина.
        /// Нужен там, где профиль ломаный: уклон по концам на таком профиле врёт.
        /// </summary>
        public static double WeightedSlope(IList<double> elevationsFromOutlet, IList<double> segmentLengths)
        {
            if (elevationsFromOutlet.Count < 2 || segmentLengths.Count != elevationsFromOutlet.Count - 1)
                return 0.0;

            double total = 0.0;
            foreach (double l in segmentLengths) total += l;
            if (total <= 0.0) return 0.0;

            double h0 = elevationsFromOutlet[0];
            double sum = 0.0;
            for (int i = 0; i < segmentLengths.Count; i++)
                sum += (elevationsFromOutlet[i] + elevationsFromOutlet[i + 1]) * segmentLengths[i];

            return (sum - 2.0 * h0 * total) / (total * total);
        }

        /// <summary>
        /// Средний уклон водосбора по градиенту рельефа в ячейках маски.
        /// Аргумент времени склонового добегания в формуле предельной интенсивности.
        /// </summary>
        public static double MeanBasinSlope(Grid g, WatershedMask mask)
        {
            double sum = 0.0;
            int n = 0;

            for (int i = 0; i < g.Count; i++)
            {
                if (!mask.Cells[i] || !g.HasData(i)) continue;
                int ix = g.Ix(i), iy = g.Iy(i);

                double dzdx = Slope1D(g, ix - 1, iy, ix + 1, iy, i);
                double dzdy = Slope1D(g, ix, iy - 1, ix, iy + 1, i);

                sum += Math.Sqrt(dzdx * dzdx + dzdy * dzdy);
                n++;
            }
            return n == 0 ? 0.0 : sum / n;
        }

        /// <summary>
        /// Центральная разность, если оба соседа есть; односторонняя на границе
        /// сетки/данных - на делитель 2*step там неоткуда взяться второй половине.
        /// </summary>
        private static double Slope1D(Grid g, int ix1, int iy1, int ix2, int iy2, int center)
        {
            bool ok1, ok2;
            double z1 = Sample(g, ix1, iy1, center, out ok1);
            double z2 = Sample(g, ix2, iy2, center, out ok2);

            if (ok1 && ok2) return (z2 - z1) / (2.0 * g.Step);
            if (ok2) return (z2 - g.Z[center]) / g.Step;
            if (ok1) return (g.Z[center] - z1) / g.Step;
            return 0.0;
        }

        private static double Sample(Grid g, int ix, int iy, int fallback, out bool valid)
        {
            if (g.Inside(ix, iy))
            {
                int j = g.Index(ix, iy);
                if (g.HasData(j)) { valid = true; return g.Z[j]; }
            }
            valid = false;
            return g.Z[fallback];
        }
    }
}
