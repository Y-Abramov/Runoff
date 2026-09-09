using System;

namespace AbrRunoff.Core.Watershed
{
    public sealed class FillReport
    {
        public int FilledCells;
        public double MaxDepth;
        public double Volume;
    }

    /// <summary>
    /// Priority-Flood с эпсилон-наклоном (Barnes, Lehman, Mulla, 2014).
    /// Заливка идёт от границы данных внутрь очередью с приоритетом; каждая ячейка
    /// поднимается минимум на eps выше той, из которой её достали. Без eps плоскости
    /// заполнения остались бы без направления стока, и следующий шаг встал бы.
    /// </summary>
    public static class DepressionFill
    {
        public static FillReport Fill(Grid g, double eps)
        {
            var report = new FillReport();
            var done = new bool[g.Count];
            var heap = new MinHeap(1024);

            // Стартуют все ячейки, граничащие с NoData или с краем сетки:
            // это и есть выходы, через которые вода покидает область.
            for (int i = 0; i < g.Count; i++)
            {
                if (!g.HasData(i)) continue;
                int ix = g.Ix(i), iy = g.Iy(i);
                bool border = ix == 0 || iy == 0 || ix == g.Nx - 1 || iy == g.Ny - 1;
                if (!border)
                {
                    for (int dy = -1; dy <= 1 && !border; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            if (!g.HasData(g.Index(ix + dx, iy + dy))) { border = true; break; }
                        }
                }
                if (border) { heap.Push(g.Z[i], i); done[i] = true; }
            }

            while (heap.Count > 0)
            {
                double z;
                int i;
                heap.Pop(out z, out i);
                int ix = g.Ix(i), iy = g.Iy(i);

                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int jx = ix + dx, jy = iy + dy;
                        if (!g.Inside(jx, jy)) continue;
                        int j = g.Index(jx, jy);
                        if (done[j] || !g.HasData(j)) continue;

                        double zj = g.Z[j];
                        double raised = z + eps;
                        if (zj < raised)
                        {
                            double d = raised - zj;
                            report.FilledCells++;
                            report.Volume += d * g.CellArea;
                            if (d > report.MaxDepth) report.MaxDepth = d;
                            g.Z[j] = raised;
                            zj = raised;
                        }
                        done[j] = true;
                        heap.Push(zj, j);
                    }
            }
            return report;
        }

        /// <summary>Двоичная куча на массивах: пары (отметка, индекс ячейки).</summary>
        private sealed class MinHeap
        {
            private double[] m_Key;
            private int[] m_Val;
            private int m_Count;

            public MinHeap(int capacity)
            {
                m_Key = new double[capacity];
                m_Val = new int[capacity];
            }

            public int Count { get { return m_Count; } }

            public void Push(double key, int val)
            {
                if (m_Count == m_Key.Length)
                {
                    Array.Resize(ref m_Key, m_Count * 2);
                    Array.Resize(ref m_Val, m_Count * 2);
                }
                int i = m_Count++;
                m_Key[i] = key; m_Val[i] = val;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (m_Key[p] <= m_Key[i]) break;
                    Swap(p, i);
                    i = p;
                }
            }

            public void Pop(out double key, out int val)
            {
                key = m_Key[0]; val = m_Val[0];
                m_Count--;
                m_Key[0] = m_Key[m_Count]; m_Val[0] = m_Val[m_Count];
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1, r = l + 1, s = i;
                    if (l < m_Count && m_Key[l] < m_Key[s]) s = l;
                    if (r < m_Count && m_Key[r] < m_Key[s]) s = r;
                    if (s == i) break;
                    Swap(s, i);
                    i = s;
                }
            }

            private void Swap(int a, int b)
            {
                double k = m_Key[a]; m_Key[a] = m_Key[b]; m_Key[b] = k;
                int v = m_Val[a]; m_Val[a] = m_Val[b]; m_Val[b] = v;
            }
        }
    }
}
