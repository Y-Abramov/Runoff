using System.Collections.Generic;

namespace AbrRunoff.Core.Watershed
{
    /// <summary>
    /// Число ячеек выше по течению, включая саму ячейку. Топологический проход
    /// очередью: рекурсия на миллионах ячеек переполнит стек гарантированно.
    /// </summary>
    public static class FlowAccumulation
    {
        public static int[] Compute(Grid g, int[] dir)
        {
            var acc = new int[g.Count];
            var indegree = new int[g.Count];

            for (int i = 0; i < g.Count; i++)
            {
                if (!g.HasData(i)) continue;
                acc[i] = 1;
                if (dir[i] >= 0) indegree[dir[i]]++;
            }

            var queue = new Queue<int>();
            for (int i = 0; i < g.Count; i++)
                if (g.HasData(i) && indegree[i] == 0) queue.Enqueue(i);

            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int d = dir[i];
                if (d < 0) continue;
                acc[d] += acc[i];
                if (--indegree[d] == 0) queue.Enqueue(d);
            }
            return acc;
        }
    }
}
