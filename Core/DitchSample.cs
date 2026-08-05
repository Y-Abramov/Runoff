namespace AbrRunoff.Core
{
    /// <summary>
    /// Один отсчёт по кювету. Голые числа, без типов Robur - ядро тестируется
    /// без установленного Robur.
    /// </summary>
    public struct DitchSample
    {
        /// <summary>Пикет, м.</summary>
        public double Station;

        /// <summary>Абсолютная отметка дна кювета, м (из DYNAMIC_LEFT_HK / DYNAMIC_RIGHT_HK).</summary>
        public double BottomZ;

        /// <summary>Поперечное смещение дна от оси, м (из LOFFSX / ROFFSX найденной точки).</summary>
        public double Offset;

        /// <summary>Кювет на этом пикете есть (бит SLOPE_FLAG_USE_DITCH_PROFILE взведён).</summary>
        public bool IsDitch;
    }
}
