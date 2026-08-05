namespace AbrRunoff.Core.Network
{
    public enum NodeKind
    {
        SegmentEnd, Watershed, Collector, PipeMouth, NetworkNode
    }

    /// <summary>Законный конец сети. Всё остальное - тупик.</summary>
    public enum OutfallKind
    {
        None,
        /// <summary>Сброс за пределы трассы - дальше не наша зона ответственности.</summary>
        BeyondAlignment,
        /// <summary>Труба в сторону от дороги.</summary>
        PipeOutward,
        /// <summary>Водоприёмный колодец или ливневая канализация.</summary>
        StormWell
    }

    public enum ElementKind
    {
        RoadDitch, NetworkDitch, EdgeTray, TelescopicTray,
        FastFlowTray, ChannelTray, SiteLine, Pipe
    }

    /// <summary>
    /// Достоверность связи. Порядок значений задаёт «слабость»: чем больше,
    /// тем менее надёжно - WorstConfidence берётся как максимум по пути.
    /// </summary>
    public enum LinkConfidence
    {
        /// <summary>Связь взята из модели (NodeConnectedDitch.ConnectedNode).</summary>
        Explicit = 0,
        /// <summary>Труба с собственными координатами и отметками концов.</summary>
        Physical = 1,
        /// <summary>Геометрическая догадка модуля.</summary>
        Inferred = 2
    }

    public enum TraceOutcome
    {
        ReachedOutfall, DeadEnd, Cycle
    }
}
