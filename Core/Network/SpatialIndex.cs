using System;
using System.Collections.Generic;

namespace AbrRunoff.Core.Network
{
    /// <summary>
    /// Сеточный индекс координат узлов.
    ///
    /// Сшивка ищет для каждого открытого конца ближайшие узлы. Перебор пар на
    /// проекте с десятком дорог даёт квадрат: узлов там десятки тысяч. Индекс
    /// сводит поиск к нескольким ячейкам вокруг точки.
    /// </summary>
    public sealed class SpatialIndex
    {
        private readonly double m_Cell;
        private readonly Dictionary<long, List<int>> m_Grid = new Dictionary<long, List<int>>();
        private readonly Dictionary<int, double[]> m_Pos = new Dictionary<int, double[]>();

        public SpatialIndex(double cellSize)
        {
            m_Cell = cellSize <= 0.0 ? 1.0 : cellSize;
        }

        public void Add(int id, double x, double y)
        {
            long key = Key(x, y);
            List<int> list;
            if (!m_Grid.TryGetValue(key, out list)) m_Grid[key] = list = new List<int>();
            list.Add(id);
            m_Pos[id] = new[] { x, y };
        }

        /// <summary>Id узлов в радиусе. Точная проверка расстояния уже сделана.</summary>
        public List<int> Query(double x, double y, double radius)
        {
            var found = new List<int>();
            int span = (int)Math.Ceiling(radius / m_Cell);
            int cx = (int)Math.Floor(x / m_Cell), cy = (int)Math.Floor(y / m_Cell);
            double r2 = radius * radius;

            for (int dx = -span; dx <= span; dx++)
                for (int dy = -span; dy <= span; dy++)
                {
                    List<int> list;
                    if (!m_Grid.TryGetValue(CellKey(cx + dx, cy + dy), out list)) continue;
                    foreach (int id in list)
                    {
                        var p = m_Pos[id];
                        double ddx = p[0] - x, ddy = p[1] - y;
                        if (ddx * ddx + ddy * ddy <= r2) found.Add(id);
                    }
                }
            return found;
        }

        private long Key(double x, double y)
        {
            return CellKey((int)Math.Floor(x / m_Cell), (int)Math.Floor(y / m_Cell));
        }

        private static long CellKey(int cx, int cy)
        {
            return ((long)cx << 32) ^ (uint)cy;
        }
    }
}
