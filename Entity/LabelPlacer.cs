using System;
using System.Collections.Generic;
using Topomatic.Cad.Foundation;
using Topomatic.Dwg.Entities;

namespace AbrRunoff.Entity
{
    /// <summary>
    /// Разводит подписи, чтобы они не наезжали друг на друга, и тянет выноску
    /// от знака к сдвинутой подписи.
    ///
    /// В Robur автоматического разведения меток нет - на плотном пикетаже подписи
    /// водоразделов и точек сбора складываются в нечитаемую кашу. Алгоритм простой
    /// намеренно: жадный сдвиг вверх (или вниз) на свободное место, детерминированный
    /// и предсказуемый - проектировщик должен понимать, почему подпись оказалась там.
    /// </summary>
    internal sealed class LabelPlacer
    {
        private struct Box
        {
            public double X0, Y0, X1, Y1;
            public bool Hits(Box o)
            {
                return !(o.X0 > X1 || o.X1 < X0 || o.Y0 > Y1 || o.Y1 < Y0);
            }
        }

        private readonly List<Box> m_Taken = new List<Box>();

        /// <summary>Максимум попыток сдвига, дальше подпись ставится как есть.</summary>
        private const int MaxTries = 14;

        /// <summary>
        /// Ставит подпись рядом с anchor. Пробует сначала предпочтительную сторону,
        /// затем ступенчато отодвигает по вертикали. Рисует выноску, если сдвиг заметный.
        /// </summary>
        internal void Place(string content, Vector2D anchor, double gap, double height,
                            CadColor color, bool background, bool preferUp, Action<DwgEntity> emit)
        {
            double width = GlyphBuilder.EstimateWidth(content, height);
            double stepY = height * 1.6;

            double baseX = anchor.X + gap;
            double baseY = anchor.Y + (preferUp ? gap * 0.5 : -gap * 0.5);

            double x = baseX, y = baseY;
            bool placed = false;

            for (int i = 0; i < MaxTries; i++)
            {
                // Чередуем вверх/вниз с нарастающим шагом: подписи расходятся
                // симметрично вокруг знака, а не уползают все в одну сторону.
                int ring = (i + 1) / 2;
                int sign = (i % 2 == 0) ? 1 : -1;
                if (!preferUp) sign = -sign;

                y = baseY + sign * ring * stepY;
                var box = new Box
                {
                    X0 = x - height * 0.3,
                    Y0 = y - height * 0.8,
                    X1 = x + width + height * 0.3,
                    Y1 = y + height * 0.8
                };

                if (!Overlaps(box))
                {
                    m_Taken.Add(box);
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                // Место не нашлось - ставим на базовую позицию, пусть лучше
                // наложится, чем подпись пропадёт.
                y = baseY;
                m_Taken.Add(new Box { X0 = x, Y0 = y, X1 = x + width, Y1 = y + height });
            }

            var target = new Vector2D(x, y);

            // Выноска нужна только когда подпись реально уехала от знака.
            if (Math.Abs(y - anchor.Y) > gap * 0.9)
            {
                GlyphBuilder.Line(anchor, new Vector2D(x - height * 0.3, y),
                                  RunoffStyle.Muted, RunoffStyle.LineThin, emit);
            }

            double unused;
            GlyphBuilder.Text(content, target, height, 0.0, color, background, emit, out unused);
        }

        /// <summary>Резервирует место без отрисовки - для знаков, чтобы подписи их обходили.</summary>
        internal void Reserve(Vector2D center, double radius)
        {
            m_Taken.Add(new Box
            {
                X0 = center.X - radius, Y0 = center.Y - radius,
                X1 = center.X + radius, Y1 = center.Y + radius
            });
        }

        private bool Overlaps(Box box)
        {
            for (int i = 0; i < m_Taken.Count; i++)
                if (m_Taken[i].Hits(box)) return true;
            return false;
        }
    }
}
