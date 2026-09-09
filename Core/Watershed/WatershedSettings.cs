namespace AbrRunoff.Core.Watershed
{
    /// <summary>
    /// Пороги расчёта водосбора. Копия хранится в примитиве, а не только в глобальных
    /// дефолтах - тот же принцип, что у RunoffSettings: старый чертёж не должен менять
    /// выводы от правки чужого дефолта.
    /// </summary>
    public sealed class WatershedSettings
    {
        /// <summary>Потолок числа ячеек сетки. Превышение поднимает шаг, а не режет область.</summary>
        public int MaxCells = 4000000;

        /// <summary>Радиус привязки створа к тальвегу, м.</summary>
        public double SnapRadius = 10.0;

        /// <summary>Минимальная аккумуляция (ячеек), при которой ячейка считается тальвегом.</summary>
        public int MinChannelCells = 50;

        /// <summary>Шаг подъёма отметки при заполнении плоскостей, м. Без него у плато нет стока.</summary>
        public double FillEpsilon = 0.001;

        /// <summary>Дырка NoData размером до стольких ячеек заполняется интерполяцией.</summary>
        public int HoleFillMaxCells = 100;

        /// <summary>Глубина заполнения впадины, выше которой пишется предупреждение, м.</summary>
        public double DeepFillWarn = 2.0;

        /// <summary>Допустимое расхождение площади упрощённого контура с маской, %.</summary>
        public double AreaTolerancePercent = 1.0;

        /// <summary>Начальный допуск упрощения контура в долях шага сетки.</summary>
        public double SimplifyToleranceCells = 1.0;
    }
}
