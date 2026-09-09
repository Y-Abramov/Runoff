using System;
using System.Collections.Generic;
using AbrRunoff.Core;
using Topomatic.Alg;
using Topomatic.Alg.Prf;

namespace AbrRunoff.Robur
{
    /// <summary>
    /// Кюветы железнодорожного пути.
    ///
    /// У ЖД пути кювет - ОТДЕЛЬНАЯ линия трассирования (Transition) со своим
    /// проектным профилем: один левый профиль и один правый (слова юзера,
    /// 2026-09-09). Это принципиально иначе, чем у автодороги, где дно ищется
    /// перебором точек типового поперечника (см. DitchReader и его самокалибровку
    /// номера точки) - поэтому отдельный читатель, а не ветка в дорожном.
    ///
    /// Что откуда (сверено по декомпиляции Topomatic.Alg, класс Transition 30166):
    ///   Transitions[0]        - ось трассы, кюветы - остальные элементы;
    ///   Transition.RedProfile - проектный профиль, дно кювета (IProfile.GetY);
    ///   Transition.Offset     - смещение линии от оси (IOffset.TryGetValue);
    ///   Transition.EgProfile  - профиль ЧЁРНОЙ ЗЕМЛИ по той же линии;
    ///   Transition.Name       - имя линии, по нему различаем стороны.
    ///
    /// Данные тут чище дорожных: отметка дна и отметка земли берутся по одной и
    /// той же линии, без догадок о номере точки поперечника.
    /// </summary>
    internal static class RailDitchReader
    {
        /// <summary>
        /// Предел правдоподобия врезки дна в землю, м - тот же смысл и то же
        /// число, что в дорожном DitchReader: глубже пяти метров это не кювет,
        /// а отметка не из той оперы.
        /// </summary>
        private const double SaneDepthLimit = 5.0;

        /// <summary>Есть ли у трассы хоть одна линия кювета с проектным профилем.</summary>
        internal static bool HasDitches(Alignment al)
        {
            return FindSide(al, DitchSide.Left) != null || FindSide(al, DitchSide.Right) != null;
        }

        /// <summary>Отсчёты одной стороны. warning != null - линия кювета этой стороны не найдена.</summary>
        internal static List<DitchSample> Read(Alignment al, DitchSide side,
                                               IList<double> stations, out string warning)
        {
            warning = null;
            var samples = new List<DitchSample>(stations.Count);

            var line = FindSide(al, side);
            if (line == null)
            {
                warning = (side == DitchSide.Left ? "Левая" : "Правая") +
                          " сторона: линия кювета с проектным профилем у пути не найдена.";
                foreach (double st in stations)
                    samples.Add(new DitchSample { Station = st, IsDitch = false });
                return samples;
            }

            IProfile bottom = SafeRedProfile(line);
            IProfile ground = SafeGroundProfile(line);

            foreach (double st in stations)
            {
                double z;
                if (bottom == null || !TryGetY(bottom, st, out z))
                {
                    // Профиль на этом пикете не определён - кювета здесь нет.
                    samples.Add(new DitchSample { Station = st, IsDitch = false });
                    continue;
                }

                double offset;
                if (!TryOffset(line, st, out offset)) offset = 0.0;

                double groundZ = 0.0;
                bool hasGround = ground != null && TryGetY(ground, st, out groundZ);

                // Тот же санитарный порог, что у автодороги: слишком глубокая
                // «врезка» - признак не рельефа, а не той отметки.
                if (hasGround && Math.Abs(groundZ - z) > SaneDepthLimit) hasGround = false;

                samples.Add(new DitchSample
                {
                    Station = st,
                    BottomZ = z,
                    Offset = offset,
                    IsDitch = true,
                    GroundZ = groundZ,
                    HasGround = hasGround
                });
            }

            return samples;
        }

        /// <summary>
        /// Линия кювета нужной стороны. Сторону определяем сперва по имени линии
        /// (проектировщик называет их «левый»/«правый»), запасной путь - по знаку
        /// смещения от оси: конвенция знака в SDK не документирована, поэтому имя
        /// вперёд, а знак - как выручалочка.
        /// </summary>
        private static Transition FindSide(Alignment al, DitchSide side)
        {
            ITransitions transitions;
            try { transitions = al == null ? null : al.Transitions; }
            catch { return null; }
            if (transitions == null) return null;

            Transition byName = null;
            Transition bySign = null;

            int count;
            try { count = transitions.Count; }
            catch { return null; }

            for (int i = 1; i < count; i++)      // 0 - ось трассы, её пропускаем
            {
                Transition t;
                try { t = transitions[i]; }
                catch { continue; }
                if (t == null || SafeRedProfile(t) == null) continue;

                DitchSide named;
                if (TrySideByName(t, out named))
                {
                    if (named == side && byName == null) byName = t;
                    continue;                     // имя сказало явно - знак не спрашиваем
                }

                DitchSide signed;
                if (TrySideBySign(t, out signed) && signed == side && bySign == null) bySign = t;
            }

            return byName ?? bySign;
        }

        private static bool TrySideByName(Transition t, out DitchSide side)
        {
            side = DitchSide.Left;
            string name;
            try { name = t.Name; }
            catch { return false; }
            if (string.IsNullOrEmpty(name)) return false;

            if (name.IndexOf("лев", StringComparison.OrdinalIgnoreCase) >= 0) { side = DitchSide.Left; return true; }
            if (name.IndexOf("прав", StringComparison.OrdinalIgnoreCase) >= 0) { side = DitchSide.Right; return true; }
            return false;
        }

        /// <summary>Отрицательное смещение - левая сторона, положительное - правая.</summary>
        private static bool TrySideBySign(Transition t, out DitchSide side)
        {
            side = DitchSide.Left;

            double station;
            if (!TryMidStation(t, out station)) return false;

            double offset;
            if (!TryOffset(t, station, out offset) || Math.Abs(offset) < 1e-9) return false;

            side = offset < 0.0 ? DitchSide.Left : DitchSide.Right;
            return true;
        }

        private static bool TryMidStation(Transition t, out double station)
        {
            station = 0.0;
            try
            {
                var al = t.Alignment;
                if (al == null) return false;
#pragma warning disable 612 // StartStation - [Obsolete] без сообщения и без замены, работает
                double start = al.StartStation;
#pragma warning restore 612
                double length = al.Plan.CompoundLine.Length;
                if (length <= 0.0) return false;
                station = start + length * 0.5;
                return true;
            }
            catch { return false; }
        }

        private static bool TryOffset(Transition t, double station, out double offset)
        {
            offset = 0.0;
            try
            {
                var off = t.Offset;
                if (off == null || off.IsEmpty) return false;

                double first, last;
                if (!off.TryGetValue(station, out first, out last)) return false;

                // На переломе смещения TryGetValue отдаёт значения до и после;
                // берём середину - дно кювета в этой точке одно.
                offset = (first + last) * 0.5;
                return true;
            }
            catch { return false; }
        }

        private static IProfile SafeRedProfile(Transition t)
        {
            try { return t.RedProfile; }
            catch { return null; }
        }

        /// <summary>Профиль земли по линии кювета: статический, иначе динамический, иначе общий.</summary>
        private static IProfile SafeGroundProfile(Transition t)
        {
            try { if (t.StaticEg != null) return t.StaticEg; } catch { }
            try { if (t.DynamicEg != null) return t.DynamicEg; } catch { }
            try { return t.EgProfile; } catch { return null; }
        }

        private static bool TryGetY(IProfile profile, double station, out double value)
        {
            value = 0.0;
            try { return profile.GetY(station, out value); }
            catch { return false; }
        }
    }
}
