using System.Collections.Generic;

namespace AbrRunoff.Core
{
    public sealed class RunoffInput
    {
        public IList<DitchSample> Left = new List<DitchSample>();
        public IList<DitchSample> Right = new List<DitchSample>();
        public RunoffSettings Settings = new RunoffSettings();
    }

    public sealed class RunoffResult
    {
        public List<FlowSegment> Segments = new List<FlowSegment>();
        public List<FlowPoint> Points = new List<FlowPoint>();

        /// <summary>Человекочитаемые предупреждения для ведомости.</summary>
        public List<string> Warnings = new List<string>();
    }
}
