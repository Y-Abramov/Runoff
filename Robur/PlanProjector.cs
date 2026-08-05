using System;
using Topomatic.Alg.Road;
using Topomatic.Cad.Foundation;

namespace AbrRunoff.Robur
{
    /// <summary>
    /// Пикет + поперечное смещение -> координаты плана.
    /// Метод найден рефлексией по Topomatic.Cad.Foundation.CompoundLine;
    /// живая проверка - гейт Task 4.
    /// </summary>
    internal sealed class PlanProjector
    {
        private readonly CompoundLine m_Line;

        internal PlanProjector(RoadAlignment road)
        {
            m_Line = road.Plan.CompoundLine;
        }

        /// <summary>Точка на оси трассы.</summary>
        internal bool TryAxis(double station, out Vector2D pos)
        {
            return TryPoint(station, 0.0, out pos);
        }

        /// <summary>Точка со смещением от оси. Знак offset задаёт сторону.</summary>
        internal bool TryPoint(double station, double offset, out Vector2D pos)
        {
            pos = Vector2D.Empty;
            try { return m_Line.StaOffsetToPos(station, offset, out pos); }
            catch { return false; }
        }

        /// <summary>
        /// Единичный вектор вдоль оси в сторону роста пикетажа. Нужен для разворота
        /// стрелок и знаков; считается конечной разностью, отдельного API нет.
        /// </summary>
        internal bool TryTangent(double station, out Vector2D dir)
        {
            dir = Vector2D.Empty;
            const double d = 0.5;
            Vector2D a, b;
            if (!TryAxis(station - d, out a) && !TryAxis(station, out a)) return false;
            if (!TryAxis(station + d, out b)) return false;

            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9) return false;
            dir = new Vector2D(dx / len, dy / len);
            return true;
        }
    }
}
