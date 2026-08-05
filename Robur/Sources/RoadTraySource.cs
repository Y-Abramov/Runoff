using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AbrRunoff.Core;
using AbrRunoff.Core.Network;
using Topomatic.Alg.Road;
using Topomatic.Cad.Foundation;

namespace AbrRunoff.Robur.Sources
{
    /// <summary>
    /// Прикромочные и телескопические лотки трассы.
    ///
    /// Состав EdgeTray/TelescopicTray живьём не подтверждён, поэтому чтение идёт
    /// рефлексией: отсутствие ожидаемого свойства даёт пустой результат, а не
    /// падение плагина. Что реально пришло - видно в ведомости по SourceRef.
    /// </summary>
    internal sealed class RoadTraySource : IDrainageSource
    {
        private readonly RoadAlignment m_Road;
        private readonly string m_Name;

        internal RoadTraySource(RoadAlignment road, string name)
        {
            m_Road = road;
            m_Name = name;
        }

        public string Name { get { return m_Name; } }

        public List<DrainageElement> Read(RunoffSettings settings)
        {
            var result = new List<DrainageElement>();
            var proj = new PlanProjector(m_Road);

            ReadCollection(result, proj, Get(m_Road, "EdgeTrays"),
                           ElementKind.EdgeTray, "прикромочный лоток");
            ReadCollection(result, proj, Get(m_Road, "TelescopicTrays"),
                           ElementKind.TelescopicTray, "телескопический лоток");
            return result;
        }

        private void ReadCollection(List<DrainageElement> result, PlanProjector proj,
                                    object collection, ElementKind kind, string caption)
        {
            var items = collection as IEnumerable;
            if (items == null) return;

            int index = 0;
            foreach (var tray in items)
            {
                index++;
                if (tray == null) continue;

                double from = ToDouble(Get(tray, "StartStation"));
                double to = ToDouble(Get(tray, "EndStation"));
                double offset = ToDouble(Get(tray, "Offset"));
                if (to <= from) continue;

                var el = new DrainageElement
                {
                    Kind = kind,
                    SourceRef = m_Name + ", " + caption + " " + index
                };

                double step = Math.Max(1.0, (to - from) / 20.0);
                for (double st = from; st <= to + 1e-9; st += step)
                {
                    Vector2D p;
                    if (!proj.TryPoint(st, offset, out p)) continue;

                    el.Samples.Add(new DitchSample
                    {
                        Station = st,
                        BottomZ = ElevationAt(tray, st, from, to),
                        Offset = offset,
                        IsDitch = true
                    });
                    el.Positions.Add(p);
                }

                if (el.Samples.Count > 1) result.Add(el);
            }
        }

        /// <summary>
        /// Отметка лотка на пикете. Профиль лотка рефлексией не достать надёжно,
        /// поэтому линейно интерполируем между отметками концов - для определения
        /// НАПРАВЛЕНИЯ стока этого достаточно, а уклон по участку выйдет средним.
        /// </summary>
        private static double ElevationAt(object tray, double station, double from, double to)
        {
            double z0 = ToDouble(Get(tray, "StartElevation"));
            double z1 = ToDouble(Get(tray, "EndElevation"));
            if (to - from < 1e-9) return z0;
            double t = (station - from) / (to - from);
            return z0 + (z1 - z0) * t;
        }

        private static object Get(object obj, string name)
        {
            if (obj == null) return null;
            try
            {
                var p = obj.GetType().GetProperty(name,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                return p == null ? null : p.GetValue(obj, null);
            }
            catch { return null; }
        }

        private static double ToDouble(object v)
        {
            try { return v == null ? 0.0 : Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture); }
            catch { return 0.0; }
        }
    }
}
