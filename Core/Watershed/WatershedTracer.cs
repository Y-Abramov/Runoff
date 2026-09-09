using System.Collections.Generic;

namespace AbrRunoff.Core.Watershed
{
    public sealed class WatershedMask
    {
        public bool[] Cells;
        public int Count;
        public double AreaM2;
        public int Outlet;

        /// <summary>Водосбор упёрся в границу данных - значит он оборван, а не найден.</summary>
        public bool TouchesEdge;
    }

    /// <summary>Обратный обход вверх по D8 от замыкающей ячейки.</summary>
    public static class WatershedTracer
    {
        public static WatershedMask Trace(Grid g, int[] dir, int outlet)
        {
            var mask = new WatershedMask
            {
                Cells = new bool[g.Count],
                Outlet = outlet
            };

            var stack = new Stack<int>();
            stack.Push(outlet);
            mask.Cells[outlet] = true;

            while (stack.Count > 0)
            {
                int i = stack.Pop();
                mask.Count++;

                int ix = g.Ix(i), iy = g.Iy(i);
                if (ix == 0 || iy == 0 || ix == g.Nx - 1 || iy == g.Ny - 1) mask.TouchesEdge = true;

                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int jx = ix + dx, jy = iy + dy;
                        if (!g.Inside(jx, jy)) continue;
                        int j = g.Index(jx, jy);

                        if (!g.HasData(j)) { mask.TouchesEdge = true; continue; }
                        if (mask.Cells[j]) continue;
                        if (dir[j] != i) continue;      // сосед стекает не сюда

                        mask.Cells[j] = true;
                        stack.Push(j);
                    }
            }

            mask.AreaM2 = mask.Count * g.CellArea;
            return mask;
        }
    }
}
