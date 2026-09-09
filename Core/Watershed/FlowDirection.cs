using System;

namespace AbrRunoff.Core.Watershed
{
    /// <summary>
    /// D8: вся вода ячейки уходит одному соседу - тому, куда круче падение на единицу
    /// расстояния. Выбран вместо D-infinity сознательно: главный лог должен быть одной
    /// линией, а не веером долей стока, и вся ветка построена вокруг этой линии.
    /// </summary>
    public static class FlowDirection
    {
        public static int[] Compute(Grid g)
        {
            var dir = new int[g.Count];
            double diag = Math.Sqrt(2.0);

            for (int i = 0; i < g.Count; i++)
            {
                dir[i] = -1;
                if (!g.HasData(i)) continue;

                int ix = g.Ix(i), iy = g.Iy(i);
                double zi = g.Z[i];
                double best = 0.0;
                int bestCell = -1;

                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int jx = ix + dx, jy = iy + dy;
                        if (!g.Inside(jx, jy)) continue;
                        int j = g.Index(jx, jy);
                        if (!g.HasData(j)) continue;

                        double drop = zi - g.Z[j];
                        if (drop <= 0.0) continue;
                        double slope = (dx != 0 && dy != 0) ? drop / diag : drop;
                        if (slope > best) { best = slope; bestCell = j; }
                    }

                dir[i] = bestCell;
            }
            return dir;
        }
    }
}
