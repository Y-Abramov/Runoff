using System;

namespace AbrRunoff.Core.Watershed
{
    /// <summary>
    /// Перенос замыкающего створа на тальвег. Не удобство, а обязательный шаг:
    /// координата оголовка трубы почти никогда не попадает точно в ячейку водотока,
    /// и без привязки водосбор выходит из трёх ячеек вместо квадратных километров.
    /// Отказ выглядит как сломанный модуль, хотя данные верны.
    /// </summary>
    public static class PourPoint
    {
        public static bool TrySnap(Grid g, int[] acc, double x, double y,
                                   WatershedSettings settings, out int cell, out double shift)
        {
            cell = -1;
            shift = 0.0;

            if (!g.TryLocate(x, y, out int cx, out int cy)) return false;

            int r = (int)Math.Ceiling(settings.SnapRadius / g.Step);
            int bestAcc = settings.MinChannelCells - 1;
            double bestDist = double.MaxValue;

            for (int iy = cy - r; iy <= cy + r; iy++)
                for (int ix = cx - r; ix <= cx + r; ix++)
                {
                    if (!g.Inside(ix, iy)) continue;
                    int i = g.Index(ix, iy);
                    if (!g.HasData(i)) continue;

                    double dx = g.CellX(ix) - x, dy = g.CellY(iy) - y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist > settings.SnapRadius) continue;

                    if (acc[i] > bestAcc || (acc[i] == bestAcc && dist < bestDist))
                    {
                        bestAcc = acc[i];
                        bestDist = dist;
                        cell = i;
                    }
                }

            if (cell < 0) return false;
            shift = bestDist;
            return true;
        }
    }
}
