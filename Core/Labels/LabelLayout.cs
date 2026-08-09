using System;
using System.Collections.Generic;

namespace AbrRunoff.Core.Labels
{
    /// <summary>Оси-выровненный охват (bounding box) в мировых координатах.</summary>
    public struct LabelBounds
    {
        public double X0, Y0, X1, Y1;

        public bool Overlaps(LabelBounds o)
        {
            return !(o.X0 > X1 || o.X1 < X0 || o.Y0 > Y1 || o.Y1 < Y0);
        }
    }

    /// <summary>Итог размещения одной подписи.</summary>
    public struct LabelPlacement
    {
        public double X, Y;
        /// <summary>Ушла ли подпись от базовой позиции - решает, рисовать ли выноску.</summary>
        public bool Moved;
    }

    /// <summary>
    /// Чистая геометрия разведения подписей внутри одной схемы: жадный сдвиг по
    /// вертикали (в мировых координатах), пока повёрнутая рамка текста не
    /// перестанет пересекаться с уже размещёнными. Ни одной ссылки на Topomatic -
    /// раскладка проверяется тестами без Robur; отрисовку и текст берёт на себя
    /// Entity/LabelPlacer (тонкая обёртка).
    ///
    /// Один экземпляр = одна схема (DwgRunoffScheme/DwgDrainageNetwork). Разные
    /// схемы на стыке дорог друг о друге не знают - известное ограничение v1.
    /// </summary>
    public sealed class LabelLayout
    {
        private readonly List<LabelBounds> m_Taken = new List<LabelBounds>();

        /// <summary>Максимум попыток сдвига, дальше подпись ставится как есть.</summary>
        private const int MaxTries = 14;

        /// <summary>Отступ рамки от текста по горизонтали, в высотах текста.</summary>
        private const double PadXRatio = 0.3;

        /// <summary>Половина высоты рамки, в высотах текста (с запасом под подложку).</summary>
        private const double HalfHeightRatio = 0.8;

        /// <summary>
        /// Ставит подпись рядом с anchor. Пробует базовую позицию, затем ступенчато
        /// отодвигает по вертикали, чередуя стороны с нарастающим шагом.
        /// </summary>
        public LabelPlacement Place(double anchorX, double anchorY, double width, double height,
                                    double rotationRad, double gap, bool preferUp)
        {
            double stepY = height * 1.6;
            double baseX = anchorX + gap;
            double baseY = anchorY + (preferUp ? gap * 0.5 : -gap * 0.5);

            double x = baseX, y = baseY;
            bool placed = false;
            LabelBounds box = default(LabelBounds);

            for (int i = 0; i < MaxTries; i++)
            {
                // Чередуем вверх/вниз с нарастающим шагом: подписи расходятся
                // симметрично вокруг знака, а не уползают все в одну сторону.
                int ring = (i + 1) / 2;
                int sign = (i % 2 == 0) ? 1 : -1;
                if (!preferUp) sign = -sign;

                y = baseY + sign * ring * stepY;
                box = Bounds(x, y, width, height, rotationRad);

                if (!Overlaps(box))
                {
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                // Место не нашлось - ставим на базовую позицию, пусть лучше
                // наложится, чем подпись пропадёт.
                y = baseY;
                box = Bounds(x, y, width, height, rotationRad);
            }

            m_Taken.Add(box);
            return new LabelPlacement { X = x, Y = y, Moved = Math.Abs(y - anchorY) > gap * 0.9 };
        }

        /// <summary>
        /// Ставит подпись в место, назначенное пользователем грипом: смещение от
        /// точки привязки применяется как есть, поиск свободного места не ведётся.
        /// Ручная правка обязана побеждать автомат - иначе оттащенная подпись
        /// возвращалась бы назад при каждой перерисовке.
        ///
        /// Место всё равно резервируется: автоматические подписи должны обходить
        /// оттащенную, а не садиться на неё.
        /// </summary>
        public LabelPlacement PlaceManual(double anchorX, double anchorY, double dx, double dy,
                                          double width, double height, double rotationRad)
        {
            double x = anchorX + dx;
            double y = anchorY + dy;

            m_Taken.Add(Bounds(x, y, width, height, rotationRad));

            // Выноска нужна, когда подпись реально отъехала от своего знака.
            bool moved = Math.Abs(dx) > 1e-9 || Math.Abs(dy) > 1e-9;
            return new LabelPlacement { X = x, Y = y, Moved = moved };
        }

        /// <summary>Резервирует место без отрисовки - для знаков, чтобы подписи их обходили.</summary>
        public void Reserve(double centerX, double centerY, double radius)
        {
            m_Taken.Add(new LabelBounds
            {
                X0 = centerX - radius,
                Y0 = centerY - radius,
                X1 = centerX + radius,
                Y1 = centerY + radius
            });
        }

        /// <summary>Охват текстовой рамки (левый край в x,y, ширина width), повёрнутой на rotationRad.</summary>
        private static LabelBounds Bounds(double x, double y, double width, double height, double rotationRad)
        {
            double padX = height * PadXRatio;
            double halfH = height * HalfHeightRatio;
            double cos = Math.Cos(rotationRad), sin = Math.Sin(rotationRad);

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            double[] cornersX = { -padX, width + padX, width + padX, -padX };
            double[] cornersY = { -halfH, -halfH, halfH, halfH };

            for (int i = 0; i < 4; i++)
            {
                double cx = x + cornersX[i] * cos - cornersY[i] * sin;
                double cy = y + cornersX[i] * sin + cornersY[i] * cos;
                if (cx < minX) minX = cx; if (cx > maxX) maxX = cx;
                if (cy < minY) minY = cy; if (cy > maxY) maxY = cy;
            }

            return new LabelBounds { X0 = minX, Y0 = minY, X1 = maxX, Y1 = maxY };
        }

        private bool Overlaps(LabelBounds box)
        {
            for (int i = 0; i < m_Taken.Count; i++)
                if (m_Taken[i].Overlaps(box)) return true;
            return false;
        }
    }
}
