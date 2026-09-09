using System.Collections.Generic;

namespace AbrRunoff.Core.Watershed
{
    /// <summary>
    /// Контур маски по рёбрам ячеек. Собираем граничные рёбра (сторона ячейки, за
    /// которой маски нет) и сшиваем их в замкнутые кольца по узлам решётки.
    /// Узлы адресуются целыми номерами (cornerId = iy * (Nx + 1) + ix): сшивка по
    /// координатам с плавающей точкой рвалась бы на ошибке округления.
    /// </summary>
    public static class BoundaryTrace
    {
        public static List<List<Pt>> TraceRings(Grid g, WatershedMask mask)
        {
            // Ребро = пара номеров углов. Храним переходы: из угла -> в угол.
            var next = new Dictionary<int, List<int>>();

            for (int iy = 0; iy < g.Ny; iy++)
                for (int ix = 0; ix < g.Nx; ix++)
                {
                    if (!mask.Cells[g.Index(ix, iy)]) continue;

                    int bl = Corner(g, ix, iy);
                    int br = Corner(g, ix + 1, iy);
                    int tr = Corner(g, ix + 1, iy + 1);
                    int tl = Corner(g, ix, iy + 1);

                    // Обход ячейки против часовой стрелки: снаружи маски остаётся слева.
                    if (!InMask(g, mask, ix, iy - 1)) Add(next, bl, br);
                    if (!InMask(g, mask, ix + 1, iy)) Add(next, br, tr);
                    if (!InMask(g, mask, ix, iy + 1)) Add(next, tr, tl);
                    if (!InMask(g, mask, ix - 1, iy)) Add(next, tl, bl);
                }

            var rings = new List<List<Pt>>();
            var used = new HashSet<long>();

            foreach (var start in new List<int>(next.Keys))
            {
                foreach (int firstTo in next[start])
                {
                    long key = Key(start, firstTo);
                    if (used.Contains(key)) continue;

                    var ring = new List<Pt>();
                    int from = start, to = firstTo;
                    ring.Add(CornerPt(g, from));

                    while (true)
                    {
                        used.Add(Key(from, to));
                        ring.Add(CornerPt(g, to));
                        if (to == start) break;

                        List<int> outs;
                        if (!next.TryGetValue(to, out outs)) break;

                        int chosen = -1;
                        foreach (int cand in outs)
                            if (!used.Contains(Key(to, cand))) { chosen = cand; break; }
                        if (chosen < 0) break;

                        from = to;
                        to = chosen;
                    }

                    if (ring.Count > 3) rings.Add(ring);
                }
            }

            rings.Sort((a, b) => System.Math.Abs(Simplify.RingArea(b))
                                   .CompareTo(System.Math.Abs(Simplify.RingArea(a))));
            return rings;
        }

        private static bool InMask(Grid g, WatershedMask mask, int ix, int iy)
        {
            return g.Inside(ix, iy) && mask.Cells[g.Index(ix, iy)];
        }

        private static int Corner(Grid g, int ix, int iy) { return iy * (g.Nx + 1) + ix; }

        private static Pt CornerPt(Grid g, int cornerId)
        {
            int ix = cornerId % (g.Nx + 1);
            int iy = cornerId / (g.Nx + 1);
            return new Pt(g.OriginX + (ix - 0.5) * g.Step, g.OriginY + (iy - 0.5) * g.Step);
        }

        private static void Add(Dictionary<int, List<int>> next, int from, int to)
        {
            List<int> list;
            if (!next.TryGetValue(from, out list)) { list = new List<int>(2); next[from] = list; }
            list.Add(to);
        }

        private static long Key(int from, int to) { return ((long)from << 32) | (uint)to; }
    }
}
