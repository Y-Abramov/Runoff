namespace AbrRunoff.Core
{
    /// <summary>Сторона земляного полотна.</summary>
    public enum DitchSide { Left, Right }

    /// <summary>
    /// Направление течения ОТНОСИТЕЛЬНО ВОЗРАСТАНИЯ ПИКЕТАЖА.
    /// Не «влево/вправо» и не по сторонам света: на кривой такой смысл поплыл бы.
    /// </summary>
    public enum FlowDirection
    {
        /// <summary>Вода течёт в сторону роста пикетажа.</summary>
        Forward,
        /// <summary>Вода течёт в сторону убывания пикетажа.</summary>
        Backward,
        /// <summary>Плато: уклон меньше эпсилона, направление не определено.</summary>
        None
    }

    public enum PointKind
    {
        /// <summary>Локальный максимум: вода расходится в обе стороны.</summary>
        Watershed,
        /// <summary>Локальный минимум: вода сходится, нужен выпуск.</summary>
        Collector,
        /// <summary>Конец диапазона кювета, через который вода уходит наружу.</summary>
        Outlet
    }

    [System.Flags]
    public enum SegmentFlags
    {
        None = 0,
        /// <summary>Уклон меньше эпсилона - вода стоит.</summary>
        ZeroGrade = 1,
        /// <summary>Уклон ненулевой, но ниже нормы.</summary>
        BelowNorm = 2
    }

    public enum ProblemKind
    {
        None,
        /// <summary>Точка сбора без водопропускной трубы в допуске.</summary>
        NoOutlet,
        /// <summary>Дно кювета не найдено ни на одной точке поперечника.</summary>
        GeometryNotFound
    }
}
