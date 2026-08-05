using System;
using System.Collections.Generic;
using System.Globalization;
using AbrRunoff.Core;
using Topomatic.Alg.Road;

namespace AbrRunoff.Robur
{
    /// <summary>
    /// Читает отсчёты дна кювета из Alignment.Parameters.
    ///
    /// Всё нужное отдаёт ОДИН вызов GetStationParams - CRS-контекст не нужен
    /// (подтверждено живым прогоном спайка 2026-08-04):
    ///   LEFT_FLAGS  бит 2 = SLOPE_FLAG_USE_DITCH_PROFILE - кювет задан профилем;
    ///   DYNAMIC_LEFT_HK  - АБСОЛЮТНАЯ отметка дна (название «высота полки Hk»
    ///                      в Dev Guide вводит в заблуждение);
    ///   LOFFSX{i}/LOFFSY{i} - смещение и отметка точки i поперечника.
    /// </summary>
    internal static class DitchReader
    {
        /// <summary>Бит RoadConsts.SLOPE_FLAG_USE_DITCH_PROFILE.</summary>
        private const int FlagUseDitchProfile = 2;

        /// <summary>Точек поперечника, среди которых ищем дно.</summary>
        private const int MaxPointIndex = 9;

        internal sealed class SideKeys
        {
            public string Flags;      // LEFT_FLAGS
            public string DynamicHk;  // DYNAMIC_LEFT_HK
            public string OffsX;      // LOFFSX{i}
            public string OffsY;      // LOFFSY{i}

            internal static SideKeys For(DitchSide side)
            {
                return side == DitchSide.Left
                    ? new SideKeys { Flags = "LEFT_FLAGS",  DynamicHk = "DYNAMIC_LEFT_HK",  OffsX = "LOFFSX",  OffsY = "LOFFSY" }
                    : new SideKeys { Flags = "RIGHT_FLAGS", DynamicHk = "DYNAMIC_RIGHT_HK", OffsX = "ROFFSX", OffsY = "ROFFSY" };
            }
        }

        /// <summary>Пикеты для сэмплирования: равномерный шаг + станции сечений + границы.</summary>
        internal static List<double> BuildStations(RoadAlignment road, double step)
        {
            var set = new SortedSet<double>();
#pragma warning disable 612 // StartStation - [Obsolete] без сообщения и без замены, работает
            double start = road.StartStation;
#pragma warning restore 612
            double end = start;
            try { end = start + road.Plan.CompoundLine.Length; }
            catch { }

            if (end <= start) return new List<double>();
            if (step < 0.5) step = 0.5;

            for (double s = start; s < end; s += step) set.Add(Math.Round(s, 4));
            set.Add(Math.Round(end, 4));

            // станции реальных поперечников: между ними профиль может ломаться
            try
            {
                var sections = road.Corridor.Sections;
                for (int i = 0; i < sections.Count; i++)
                {
                    double st = sections[i].Station;
                    if (st >= start && st <= end) set.Add(Math.Round(st, 4));
                }
            }
            catch { }

            return new List<double>(set);
        }

        /// <summary>
        /// Быстрая проверка «есть ли у дороги кювет вообще» для списка выбора.
        /// Полное сэмплирование всех дорог проекта повесило бы диалог, поэтому
        /// пробуем 20 станций. Цена: кювет короче 1/20 длины трассы пропустится.
        /// </summary>
        internal static bool HasAnyDitch(RoadAlignment road)
        {
#pragma warning disable 612 // StartStation - [Obsolete] без сообщения и без замены, работает
            double start = road.StartStation;
#pragma warning restore 612
            double end = start;
            try { end = start + road.Plan.CompoundLine.Length; }
            catch { return false; }
            if (end <= start) return false;

            for (int i = 0; i <= 20; i++)
            {
                double st = start + (end - start) * i / 20.0;
                var p = GetParams(road, st);
                if (p == null) continue;
                if (HasFlag(p, "LEFT_FLAGS") || HasFlag(p, "RIGHT_FLAGS")) return true;
            }
            return false;
        }

        /// <summary>Читает отсчёты одной стороны. warning != null - геометрия дна не найдена.</summary>
        internal static List<DitchSample> Read(RoadAlignment road, DitchSide side,
                                               IList<double> stations, out string warning)
        {
            warning = null;
            var keys = SideKeys.For(side);
            var samples = new List<DitchSample>(stations.Count);
            int calibratedIndex = -1;
            bool sawDitch = false;

            foreach (double st in stations)
            {
                var p = GetParams(road, st);
                if (p == null) continue;

                bool isDitch = HasFlag(p, keys.Flags);
                if (!isDitch)
                {
                    samples.Add(new DitchSample { Station = st, IsDitch = false });
                    continue;
                }
                sawDitch = true;

                double bottomZ = GetDouble(p, keys.DynamicHk);

                // Самокалибровка: номер точки дна зависит от типового поперечника,
                // жёстко зашивать 9 нельзя. Ищем ту точку, чья отметка совпала
                // с DYNAMIC_*_HK, и берём её смещение.
                int idx = calibratedIndex;
                if (idx < 0 || !MatchesBottom(p, keys, idx, bottomZ))
                    idx = FindBottomIndex(p, keys, bottomZ);

                if (idx < 0)
                {
                    samples.Add(new DitchSample { Station = st, IsDitch = false });
                    continue;
                }
                calibratedIndex = idx;

                samples.Add(new DitchSample
                {
                    Station = st,
                    BottomZ = bottomZ,
                    Offset  = GetDouble(p, keys.OffsX + idx.ToString(CultureInfo.InvariantCulture)),
                    IsDitch = true
                });
            }

            if (sawDitch && calibratedIndex < 0)
                warning = (side == DitchSide.Left ? "Левая" : "Правая") +
                          " сторона: геометрия дна кювета не найдена ни на одной точке поперечника.";

            return samples;
        }

        private static bool MatchesBottom(IDictionary<string, object> p, SideKeys keys, int idx, double bottomZ)
        {
            return Math.Abs(GetDouble(p, keys.OffsY + idx.ToString(CultureInfo.InvariantCulture)) - bottomZ) < 1e-6;
        }

        private static int FindBottomIndex(IDictionary<string, object> p, SideKeys keys, double bottomZ)
        {
            for (int i = MaxPointIndex; i >= 1; i--)      // с конца: дно - крайняя точка полотна
                if (MatchesBottom(p, keys, i, bottomZ)) return i;
            return -1;
        }

        private static IDictionary<string, object> GetParams(RoadAlignment road, double station)
        {
            try { return road.Parameters.GetStationParams<object>(station); }
            catch { return null; }
        }

        private static bool HasFlag(IDictionary<string, object> p, string key)
        {
            object v;
            if (p == null || !p.TryGetValue(key, out v) || v == null) return false;
            try { return (Convert.ToInt32(v, CultureInfo.InvariantCulture) & FlagUseDitchProfile) != 0; }
            catch { return false; }
        }

        private static double GetDouble(IDictionary<string, object> p, string key)
        {
            object v;
            if (p == null || !p.TryGetValue(key, out v) || v == null) return 0.0;
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); }
            catch { return 0.0; }
        }
    }
}
