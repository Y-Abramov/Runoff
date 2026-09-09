using System.Collections.Generic;
using AbrRunoff.Core.Watershed;
using Topomatic.Cad.Foundation;
using Topomatic.Sfc;
using Topomatic.Sfc.Sections;

namespace AbrRunoff.Robur
{
    internal struct ProfilePoint
    {
        public double Distance;
        public double Z;
    }

    internal static class LogProfile
    {
        /// <summary>
        /// Продольный профиль лога штатным API поверхности. Сетка даёт отметку,
        /// осреднённую по ячейке; CreateSection снимает её с самой триангуляции.
        /// Отказ API - не повод ронять команду: профиль из сетки уже посчитан.
        ///
        /// pathFromOutlet и gridElevations - в одном порядке: от створа (индекс 0)
        /// вверх к истоку, том же, в котором Metrics.WeightedSlope ждёт отметки.
        /// SectionNode.Vertex.X - расстояние вдоль сечения (Section сортирует узлы
        /// по нему), Vertex.Y - отметка: подтверждено декомпиляцией Topomatic.Sfc.
        /// </summary>
        internal static List<ProfilePoint> Build(Surface surface, IList<Pt> pathFromOutlet,
                                                 IList<double> gridElevations)
        {
            var result = new List<ProfilePoint>();
            var pts = new List<Vector2D>(pathFromOutlet.Count);
            foreach (var p in pathFromOutlet) pts.Add(new Vector2D(p.X, p.Y));

            try
            {
                var section = surface.CreateSection(pts, SectionFlags.None);
                if (section != null) FillFromSection(section, result);
            }
            catch (System.Exception)
            {
                result.Clear();
            }

            if (result.Count > 0) return result;

            double dist = 0.0;
            for (int i = 0; i < pathFromOutlet.Count && i < gridElevations.Count; i++)
            {
                if (i > 0)
                {
                    double dx = pathFromOutlet[i].X - pathFromOutlet[i - 1].X;
                    double dy = pathFromOutlet[i].Y - pathFromOutlet[i - 1].Y;
                    dist += System.Math.Sqrt(dx * dx + dy * dy);
                }
                result.Add(new ProfilePoint { Distance = dist, Z = gridElevations[i] });
            }
            return result;
        }

        private static void FillFromSection(Section section, List<ProfilePoint> result)
        {
            foreach (SectionNode n in section)
                result.Add(new ProfilePoint { Distance = n.Vertex.X, Z = n.Vertex.Y });
        }
    }
}
