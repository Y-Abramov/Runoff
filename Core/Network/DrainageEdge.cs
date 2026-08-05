namespace AbrRunoff.Core.Network
{
    /// <summary>Направленное ребро: вода течёт от FromNode к ToNode.</summary>
    public sealed class DrainageEdge
    {
        public int Id;
        public int FromNode, ToNode;
        public double Length;
        public double GradePermille;
        public ElementKind Element;
        public LinkConfidence Confidence;

        /// <summary>Ссылка на исходный объект - для ведомости и подсветки.</summary>
        public string SourceRef = "";
    }
}
