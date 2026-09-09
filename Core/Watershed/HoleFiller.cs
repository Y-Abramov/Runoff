using System.Collections.Generic;

namespace AbrRunoff.Core.Watershed
{
    /// <summary>
    /// Мелкие пропуски внутри поверхности. Дырка, оставленная как есть, работает
    /// стоком в никуда и режет водосбор пополам; крупную закрывать нельзя - там
    /// данных действительно нет, и об этом должен узнать пользователь.
    /// </summary>
    public static class HoleFiller
    {
        public static int FillSmallHoles(Grid g, int maxCells)
        {
            var seen = new bool[g.Count];
            var component = new List<int>();
            var queue = new Queue<int>();
            int filledTotal = 0;

            for (int start = 0; start < g.Count; start++)
            {
                if (seen[start] || g.HasData(start)) continue;

                component.Clear();
                queue.Clear();
                queue.Enqueue(start);
                seen[start] = true;
                bool touchesEdge = false;

                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    component.Add(i);
                    int ix = g.Ix(i), iy = g.Iy(i);
                    if (ix == 0 || iy == 0 || ix == g.Nx - 1 || iy == g.Ny - 1) touchesEdge = true;

                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int jx = ix + dx, jy = iy + dy;
                            if (!g.Inside(jx, jy)) continue;
                            int j = g.Index(jx, jy);
                            if (seen[j] || g.HasData(j)) continue;
                            seen[j] = true;
                            queue.Enqueue(j);
                        }
                }

                // Пропуск, выходящий на край сетки, - это не дырка, а область
                // за пределами поверхности. Её закрывать нельзя.
                if (touchesEdge || component.Count > maxCells) continue;

                double sum = 0.0;
                int n = 0;
                foreach (int i in component)
                {
                    int ix = g.Ix(i), iy = g.Iy(i);
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int jx = ix + dx, jy = iy + dy;
                            if (!g.Inside(jx, jy)) continue;
                            int j = g.Index(jx, jy);
                            if (!g.HasData(j)) continue;
                            sum += g.Z[j];
                            n++;
                        }
                }
                if (n == 0) continue;

                double avg = sum / n;
                foreach (int i in component) g.Z[i] = avg;
                filledTotal += component.Count;
            }
            return filledTotal;
        }
    }
}
