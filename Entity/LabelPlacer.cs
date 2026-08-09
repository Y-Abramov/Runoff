using System;
using System.Collections.Generic;
using AbrRunoff.Core.Labels;
using Topomatic.Cad.Foundation;
using Topomatic.Dwg.Entities;

namespace AbrRunoff.Entity
{
    /// <summary>
    /// Тонкая обёртка над Core.Labels.LabelLayout: конвертирует Vector2D/DwgEntity
    /// на границе, применяет ручные смещения и рисует текст с выноской. Сама
    /// геометрия разведения живёт в Core - без Robur, тестируется.
    ///
    /// Порядок приоритетов при размещении подписи:
    ///   1) ручное смещение проектировщика (грип) - побеждает всегда;
    ///   2) автоматическое разведение вокруг точки привязки;
    ///   3) базовая позиция, если свободного места не нашлось (лучше наложится,
    ///      чем подпись пропадёт).
    ///
    /// Один экземпляр = одна схема. Подписи ДВУХ схем (соседние дороги на стыке)
    /// друг о друге не знают - известное ограничение; на стыке разводит рука
    /// через грипы.
    /// </summary>
    internal sealed class LabelPlacer
    {
        private readonly LabelLayout m_Layout = new LabelLayout();
        private readonly LabelOffsets m_Offsets;
        private readonly List<PlacedLabel> m_Placed;

        /// <param name="offsets">Ручные смещения примитива; null - их нет.</param>
        /// <param name="placed">Приёмник размещённых подписей для грипов; null - не собирать.</param>
        internal LabelPlacer(LabelOffsets offsets, List<PlacedLabel> placed)
        {
            m_Offsets = offsets;
            m_Placed = placed;
        }

        /// <summary>
        /// Ставит подпись рядом с anchor, при необходимости повёрнутую на rotationRad
        /// (0 = горизонтально, как подписи точек; ненулевая - вдоль оси кювета).
        /// </summary>
        internal void Place(string key, string content, Vector2D anchor, double gap, double height,
                            CadColor color, bool preferUp, Action<DwgEntity> emit,
                            double rotationRad = 0.0)
        {
            double width = GlyphBuilder.EstimateWidth(content, height);

            double dx, dy;
            LabelPlacement placed;

            if (m_Offsets != null && m_Offsets.TryGet(key, out dx, out dy))
                placed = m_Layout.PlaceManual(anchor.X, anchor.Y, dx, dy, width, height, rotationRad);
            else
                placed = m_Layout.Place(anchor.X, anchor.Y, width, height, rotationRad, gap, preferUp);

            var target = new Vector2D(placed.X, placed.Y);

            // Выноска: без неё оттащенная подпись теряет связь со своим знаком.
            if (placed.Moved)
            {
                var dir = new Vector2D(Math.Cos(rotationRad), Math.Sin(rotationRad));
                GlyphBuilder.Line(anchor,
                                  new Vector2D(target.X - dir.X * height * 0.3, target.Y - dir.Y * height * 0.3),
                                  RunoffStyle.Muted, RunoffStyle.LineThin, emit);
            }

            double unused;
            GlyphBuilder.Text(content, target, height, rotationRad, color, emit, out unused);

            if (m_Placed != null)
                m_Placed.Add(new PlacedLabel
                {
                    Key = key,
                    AnchorX = anchor.X, AnchorY = anchor.Y,
                    X = target.X, Y = target.Y
                });
        }

        /// <summary>Резервирует место без отрисовки - для знаков, чтобы подписи их обходили.</summary>
        internal void Reserve(Vector2D center, double radius)
        {
            m_Layout.Reserve(center.X, center.Y, radius);
        }
    }
}
