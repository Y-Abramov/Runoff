namespace AbrRunoff.Core
{
    /// <summary>Характерная точка стока: водораздел, точка сбора или выход за пределы участка.</summary>
    public sealed class FlowPoint
    {
        public DitchSide Side;
        public double Station;
        public double BottomZ;
        public PointKind Kind;

        /// <summary>Пикет найденной трубы; null - трубы нет или не искали.</summary>
        public double? PipeStation;

        /// <summary>Диаметр найденной трубы, м.</summary>
        public double? PipeDiameter;

        public ProblemKind Problem;
    }
}
