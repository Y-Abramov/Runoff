using System.Collections.Generic;
using AbrRunoff.Core;
using AbrRunoff.Core.Network;
using Topomatic.Alg.Road;
using Topomatic.Cad.Foundation;

namespace AbrRunoff.Robur.Sources
{
    /// <summary>
    /// Кюветы земполотна дороги. Обёртка над проверенным DitchReader из v1 -
    /// читающий код не переписывается, меняется только упаковка результата.
    /// </summary>
    internal sealed class RoadDitchSource : IDrainageSource
    {
        private readonly RoadAlignment m_Road;
        private readonly string m_Name;

        internal RoadDitchSource(RoadAlignment road, string name)
        {
            m_Road = road;
            m_Name = name;
        }

        public string Name { get { return m_Name; } }

        public List<DrainageElement> Read(RunoffSettings settings)
        {
            var result = new List<DrainageElement>();
            var stations = DitchReader.BuildStations(m_Road, settings.SampleStep);
            if (stations.Count == 0) return result;

            var proj = new PlanProjector(m_Road);

            AddSide(result, proj, stations, DitchSide.Left, "лево");
            AddSide(result, proj, stations, DitchSide.Right, "право");
            return result;
        }

        private void AddSide(List<DrainageElement> result, PlanProjector proj,
                             IList<double> stations, DitchSide side, string sideName)
        {
            string warning;
            var samples = DitchReader.Read(m_Road, side, stations, out warning);

            var el = new DrainageElement
            {
                Kind = ElementKind.RoadDitch,
                SourceRef = m_Name + ", кювет " + sideName
            };

            foreach (var s in samples)
            {
                Vector2D p;
                if (!s.IsDitch || !proj.TryPoint(s.Station, s.Offset, out p))
                {
                    // Разрыв кювета сохраняем: FlowDetector режет по нему диапазоны.
                    el.Samples.Add(new DitchSample { Station = s.Station, IsDitch = false });
                    el.Positions.Add(Vector2D.Empty);
                    continue;
                }
                el.Samples.Add(s);
                el.Positions.Add(p);
            }

            if (el.Samples.Count > 0) result.Add(el);
        }
    }
}
