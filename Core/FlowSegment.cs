namespace AbrRunoff.Core
{
    /// <summary>Непрерывный участок с одним направлением стока.</summary>
    public sealed class FlowSegment
    {
        public DitchSide Side;
        public double StationFrom;
        public double StationTo;
        public FlowDirection Direction;

        /// <summary>Средний уклон участка, промилле (по модулю).</summary>
        public double AvgGradePermille;

        /// <summary>Минимальный уклон внутри участка, промилле (по модулю) - худшее место.</summary>
        public double MinGradePermille;

        public SegmentFlags Flags;

        public double Length { get { return StationTo - StationFrom; } }
    }
}
