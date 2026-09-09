using System;
using System.Collections.Generic;

namespace AbrRunoff.Core.Watershed
{
    /// <summary>
    /// Главный лог. По Указаниям это тальвег от замыкающего створа до САМОЙ УДАЛЁННОЙ
    /// точки водораздела, то есть самый длинный путь стока, а не самый водоносный.
    /// Считаем накопленную длину вверх топологическим проходом, затем спускаемся
    /// от створа, каждый раз выбирая приток с наибольшей накопленной длиной.
    /// </summary>
    public static class MainPath
    {
        public static List<int> Compute(Grid g, int[] dir, WatershedMask mask, int outlet)
        {
            double diag = Math.Sqrt(2.0);
            var up = new double[g.Count];          // длина самого длинного пути ВЫШЕ ячейки
            var indegree = new int[g.Count];

            for (int i = 0; i < g.Count; i++)
                if (mask.Cells[i] && dir[i] >= 0 && mask.Cells[dir[i]]) indegree[dir[i]]++;

            var queue = new Queue<int>();
            for (int i = 0; i < g.Count; i++)
                if (mask.Cells[i] && indegree[i] == 0) queue.Enqueue(i);

            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int d = dir[i];
                if (d < 0 || !mask.Cells[d]) continue;

                double step = IsDiagonal(g, i, d) ? g.Step * diag : g.Step;
                double candidate = up[i] + step;
                if (candidate > up[d]) up[d] = candidate;

                if (--indegree[d] == 0) queue.Enqueue(d);
            }

            // Спуск от створа вверх по самому длинному притоку.
            var reversed = new List<int>();
            int cur = outlet;
            var visited = new HashSet<int>();

            while (true)
            {
                reversed.Add(cur);
                if (!visited.Add(cur)) break;

                int ix = g.Ix(cur), iy = g.Iy(cur);
                int best = -1;
                double bestLen = -1.0;

                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int jx = ix + dx, jy = iy + dy;
                        if (!g.Inside(jx, jy)) continue;
                        int j = g.Index(jx, jy);
                        if (!mask.Cells[j] || dir[j] != cur) continue;

                        double step = (dx != 0 && dy != 0) ? g.Step * diag : g.Step;
                        double len = up[j] + step;
                        if (len > bestLen) { bestLen = len; best = j; }
                    }

                if (best < 0) break;
                cur = best;
            }

            reversed.Reverse();
            return reversed;
        }

        private static bool IsDiagonal(Grid g, int a, int b)
        {
            return g.Ix(a) != g.Ix(b) && g.Iy(a) != g.Iy(b);
        }
    }
}
