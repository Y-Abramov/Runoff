using System.Collections.Generic;

namespace AbrRunoff.Core.Network
{
    public sealed class TraceResult
    {
        /// <summary>Id рёбер по порядку прохождения.</summary>
        public List<int> Path = new List<int>();

        public double TotalLength;
        public double WorstGradePermille = double.MaxValue;

        /// <summary>Цепочка не надёжнее слабейшего звена.</summary>
        public LinkConfidence WorstConfidence = LinkConfidence.Explicit;

        public TraceOutcome Outcome = TraceOutcome.DeadEnd;

        /// <summary>Узел выпуска; -1 если не дошло.</summary>
        public int OutfallNode = -1;

        /// <summary>По пути была развилка, ведущая к ДРУГОМУ выпуску.</summary>
        public bool Splits;
    }
}
