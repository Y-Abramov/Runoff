using System.Collections.Generic;
using AbrRunoff.Core;
using Topomatic.Alg.Road;

namespace AbrRunoff.Robur
{
    /// <summary>
    /// Водопропускные трубы дороги в терминах ядра.
    /// Alignment.Pipes : PipesCollection - Count + Pipe this[int];
    /// Pipe: Station, Diameter, Width, Height (подтверждено рефлексией SDK 16.0.62.12).
    /// </summary>
    internal static class PipeMatcher
    {
        internal static List<PipeRef> Read(RoadAlignment road)
        {
            var list = new List<PipeRef>();
            try
            {
                var pipes = road.Pipes;
                if (pipes == null) return list;
                for (int i = 0; i < pipes.Count; i++)
                {
                    var p = pipes[i];
                    if (p == null) continue;

                    // Круглая труба - Diameter; прямоугольная его не имеет, берём Height.
                    double size = p.Diameter;
                    if (size <= 0.0) size = p.Height;

                    list.Add(new PipeRef { Station = p.Station, Diameter = size });
                }
            }
            catch { }
            return list;
        }
    }
}
