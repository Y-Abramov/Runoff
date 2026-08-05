namespace AbrRunoff.Core.Network
{
    /// <summary>Точка сети: конец элемента, экстремум, узел ливневки, устье трубы.</summary>
    public sealed class DrainageNode
    {
        public int Id;
        public double X, Y;      // координата плана
        public double Z;         // отметка
        public NodeKind Kind;
        public OutfallKind Outfall;

        /// <summary>Человекочитаемая ссылка на исходный объект - для ведомости.</summary>
        public string SourceRef = "";
    }
}
