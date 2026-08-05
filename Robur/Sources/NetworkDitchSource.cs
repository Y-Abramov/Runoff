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
    /// Кюветы модели Инженерных сетей (Topomatic.Pipes.DitchFolder).
    ///
    /// Читается рефлексией: жёсткая ссылка на Topomatic.Pipes.dll уронила бы
    /// загрузку плагина в конфигурациях, где этой сборки нет.
    ///
    /// Ditch.Positions отдаёт IList&lt;Vector3D&gt; - готовый ряд вершин с отметками.
    /// Станция = накопленная длина вдоль кювета.
    /// </summary>
    internal sealed class NetworkDitchSource : IDrainageSource
    {
        private readonly object m_PipeNetwork;
        private readonly string m_Name;

        /// <summary>Кювет -> Id узла ливневки, если связь объявлена в модели.</summary>
        internal Dictionary<string, uint> ExplicitNodeLinks = new Dictionary<string, uint>();

        internal NetworkDitchSource(object pipeNetwork, string name)
        {
            m_PipeNetwork = pipeNetwork;
            m_Name = name;
        }

        public string Name { get { return m_Name; } }

        public List<DrainageElement> Read(RunoffSettings settings)
        {
            var result = new List<DrainageElement>();
            var ditches = Get(m_PipeNetwork, "Ditches") as IEnumerable;
            if (ditches == null) return result;

            int index = 0;
            foreach (var ditch in ditches)
            {
                index++;
                if (ditch == null) continue;

                var positions = Get(ditch, "Positions") as IEnumerable;
                if (positions == null) continue;

                string name = (Get(ditch, "Name") as string) ?? ("кювет " + index);
                var el = new DrainageElement
                {
                    Kind = ElementKind.NetworkDitch,
                    SourceRef = m_Name + ", " + name
                };

                double run = 0.0;
                Vector3D prev = Vector3D.Empty;
                bool first = true;

                foreach (var raw in positions)
                {
                    if (!(raw is Vector3D)) continue;
                    var v = (Vector3D)raw;

                    if (!first)
                    {
                        double dx = v.X - prev.X, dy = v.Y - prev.Y;
                        run += Math.Sqrt(dx * dx + dy * dy);
                    }
                    first = false;
                    prev = v;

                    el.Samples.Add(new DitchSample
                    {
                        Station = run, BottomZ = v.Z, Offset = 0.0, IsDitch = true
                    });
                    el.Positions.Add(new Vector2D(v.X, v.Y));
                }

                if (el.Samples.Count < 2) continue;

                // Явная связь с узлом ливневки - её нельзя терять: она достовернее
                // любой геометрической догадки на том же стыке.
                object node = Get(ditch, "ConnectedNode");
                if (node != null)
                {
                    try { ExplicitNodeLinks[el.SourceRef] = Convert.ToUInt32(Get(node, "Id")); }
                    catch { }
                }

                result.Add(el);
            }
            return result;
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
    }
}
