using System;
using System.Collections.Generic;
using AbrRunoff.Core;
using AbrRunoff.Entity;
using AbrRunoff.Robur.Sources;
using Topomatic.Cad.Foundation;

namespace AbrRunoff.Robur
{
    /// <summary>
    /// Собирает элементы и трубы по списку источников, записанных в объекте сети.
    /// Модель, которой нет в проекте, молча пропускается: сеть должна строиться
    /// по тому, что открыто, а не падать из-за закрытой площадки.
    /// </summary>
    internal static class NetworkSourceCollector
    {
        internal static void Collect(DwgDrainageNetwork owner,
                                     List<DrainageElement> elements,
                                     List<PipeEdgeInfo> pipes)
        {
            var settings = owner.Settings;

            foreach (var rr in RoadAccess.GetOpenRoads())
            {
                if (!owner.RoadNames.Contains(rr.Name)) continue;

                elements.AddRange(new RoadDitchSource(rr.Road, rr.Name).Read(settings));
                elements.AddRange(new RoadTraySource(rr.Road, rr.Name).Read(settings));
                CollectPipes(pipes, rr);
            }
        }

        /// <summary>
        /// Трубы дороги приводятся к геометрии плана: положение обоих концов и
        /// отметки лотков. Правило сторон подтверждается дампом Task 0.
        /// </summary>
        private static void CollectPipes(List<PipeEdgeInfo> pipes, RoadAccess.RoadRef rr)
        {
            var road = rr.Road;
            var coll = road.Pipes;
            if (coll == null) return;

            var proj = new PlanProjector(road);

            for (int i = 0; i < coll.Count; i++)
            {
                var p = coll[i];
                if (p == null) continue;

                Vector2D axis, tangent;
                if (!proj.TryAxis(p.Station, out axis)) continue;
                if (!proj.TryTangent(p.Station, out tangent)) continue;

                double ca = Math.Cos(p.Angle), sa = Math.Sin(p.Angle);
                var dir = new Vector2D(tangent.X * ca - tangent.Y * sa,
                                       tangent.X * sa + tangent.Y * ca);

                pipes.Add(new PipeEdgeInfo
                {
                    Start = new Vector2D(axis.X - dir.X * p.LengthToStart,
                                         axis.Y - dir.Y * p.LengthToStart),
                    End = new Vector2D(axis.X + dir.X * p.LengthToEnd,
                                       axis.Y + dir.Y * p.LengthToEnd),
                    StartZ = p.ElevationStart,
                    EndZ = p.ElevationEnd,
                    Diameter = p.Diameter,
                    SourceRef = rr.Name + ", труба ПК" +
                                p.Station.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)
                });
            }
        }
    }
}
