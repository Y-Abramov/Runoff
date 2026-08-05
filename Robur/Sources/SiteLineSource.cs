using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AbrRunoff.Core;
using AbrRunoff.Core.Network;
using Topomatic.Cad.Foundation;

namespace AbrRunoff.Robur.Sources
{
    /// <summary>
    /// Канавы, сделанные структурными линиями модели Площадка.
    ///
    /// Принципиальное отличие от остальных источников: линия НЕ ЗНАЕТ, что она
    /// канава. Отбор задаёт пользователь - явным выбором (список индексов) или
    /// фильтром по AreaCode/Description для тех, кто ведёт коды дисциплинированно.
    /// Без отбора источник не берёт ничего: угадывать назначение линий нельзя.
    /// </summary>
    internal sealed class SiteLineSource : IDrainageSource
    {
        private readonly object m_Surface;
        private readonly string m_Name;
        private readonly HashSet<int> m_Picked;
        private readonly int m_FilterAreaCode;      // -1 = не фильтровать
        private readonly string m_FilterMask;       // null/пусто = не фильтровать

        internal SiteLineSource(object surface, string name, IEnumerable<int> pickedIndices,
                                int filterAreaCode, string filterMask)
        {
            m_Surface = surface;
            m_Name = name;
            m_Picked = pickedIndices == null ? new HashSet<int>() : new HashSet<int>(pickedIndices);
            m_FilterAreaCode = filterAreaCode;
            m_FilterMask = filterMask;
        }

        public string Name { get { return m_Name; } }

        public List<DrainageElement> Read(RunoffSettings settings)
        {
            var result = new List<DrainageElement>();
            var lines = Get(m_Surface, "StructureLines") as IEnumerable;
            if (lines == null) return result;

            int index = -1;
            foreach (var line in lines)
            {
                index++;
                if (line == null || !Accept(line, index)) continue;

                int count = ToInt(Get(line, "Count"));
                if (count < 2) continue;

                var getPos = line.GetType().GetMethod("GetPosition", new[] { typeof(int) });
                if (getPos == null) continue;

                string descr = (Get(line, "Description") as string) ?? "";
                var el = new DrainageElement
                {
                    Kind = ElementKind.SiteLine,
                    SourceRef = m_Name + ", линия " + index + (descr.Length > 0 ? " (" + descr + ")" : "")
                };

                double run = 0.0;
                Vector3D prev = Vector3D.Empty;

                for (int i = 0; i < count; i++)
                {
                    Vector3D v;
                    try { v = (Vector3D)getPos.Invoke(line, new object[] { i }); }
                    catch { continue; }

                    if (i > 0)
                    {
                        double dx = v.X - prev.X, dy = v.Y - prev.Y;
                        run += Math.Sqrt(dx * dx + dy * dy);
                    }
                    prev = v;

                    el.Samples.Add(new DitchSample
                    {
                        Station = run, BottomZ = v.Z, Offset = 0.0, IsDitch = true
                    });
                    el.Positions.Add(new Vector2D(v.X, v.Y));
                }

                if (el.Samples.Count > 1) result.Add(el);
            }
            return result;
        }

        private bool Accept(object line, int index)
        {
            if (m_Picked.Contains(index)) return true;

            if (m_FilterAreaCode >= 0 && ToInt(Get(line, "AreaCode")) == m_FilterAreaCode) return true;

            if (!string.IsNullOrEmpty(m_FilterMask))
            {
                string descr = (Get(line, "Description") as string) ?? "";
                if (descr.IndexOf(m_FilterMask, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
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

        private static int ToInt(object v)
        {
            try { return v == null ? 0 : Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture); }
            catch { return 0; }
        }
    }
}
