namespace AbrRunoff.Core
{
    /// <summary>
    /// Чем закрашивать зону бассейна. Прозрачная заливка читается лучше всего,
    /// но полагается на поддержку прозрачности при отрисовке и печати; штриховка
    /// работает всегда и различает бассейны наклоном - это запасной путь, а не
    /// украшение. Выключение оставлено для плотных планов, где зона мешает.
    /// </summary>
    public enum BasinZoneStyle
    {
        None = 0,
        Transparent = 1,
        Hatched = 2
    }

    /// <summary>
    /// Пороги и шаги анализа. Копия живёт в каждом объекте схемы, а не только
    /// в глобальных дефолтах: два юзера с разными нормами не должны спорить,
    /// а старый проект не должен менять выводы от правки чужого дефолта.
    /// </summary>
    public sealed class RunoffSettings
    {
        /// <summary>Минимальный продольный уклон дна кювета, промилле (СП 34.13330).</summary>
        public double MinGradePermille = 5.0;

        /// <summary>Ниже этого уклона участок считается плато, промилле.</summary>
        public double PlateauEpsPermille = 0.2;

        /// <summary>Допуск поиска трубы у точки сбора по пикету, м.</summary>
        public double PipeTolerance = 15.0;

        /// <summary>Шаг сэмплирования отметок дна, м.</summary>
        public double SampleStep = 5.0;

        /// <summary>
        /// Минимальная врезка дна кювета в землю, м. Мельче - кювета физически нет
        /// (проектировщик задрал профиль, чтобы конструкция не строилась), и участок
        /// считается разрывом, а не водоразделом. Работает только когда известна
        /// отметка земли.
        /// </summary>
        public double MinDitchDepth = 0.05;

        /// <summary>Шаг стрелок направления на плане, м.</summary>
        public double ArrowStep = 25.0;

        // ── Оформление ──────────────────────────────────────────────────────
        // Размеры в метрах плана. Схема живёт в одном чертеже с проектом, поэтому
        // «правильного» размера нет - он зависит от масштаба листа. Отсюда явные
        // множители вместо констант в коде.

        /// <summary>Множитель размера знаков (водораздел, точка сбора, стрелки).</summary>
        public double GlyphScale = 1.0;

        /// <summary>Высота подписей, м плана.</summary>
        public double TextHeight = 1.5;

        // Подложки под подписями (белый прямоугольник) больше НЕТ: она рисовалась
        // отдельным примитивом перед своим текстом и накрывала текст соседней
        // подписи - на плотном узле выходила россыпь белых блоков без текста.
        // Читаемость держат разведение подписей и ручной оттаск грипом.

        /// <summary>Подписи характерных точек (водораздел, сбор, сброс).</summary>
        public bool ShowPointLabels = true;

        /// <summary>Подписи уклона вдоль участков.</summary>
        public bool ShowGradeLabels = true;

        /// <summary>Как показывать зону бассейна на плане.</summary>
        public BasinZoneStyle BasinZone = BasinZoneStyle.Transparent;

        /// <summary>Непрозрачность ленты бассейна, %. Мало - не видно, много - прячет план.</summary>
        public double BasinZoneOpacity = 30.0;

        /// <summary>Ширина ленты бассейна в размерах знака. Лента идёт ВДОЛЬ линий сети.</summary>
        public double BasinBandWidth = 2.5;

        public RunoffSettings Clone()
        {
            return new RunoffSettings
            {
                MinGradePermille   = MinGradePermille,
                PlateauEpsPermille = PlateauEpsPermille,
                PipeTolerance      = PipeTolerance,
                SampleStep         = SampleStep,
                MinDitchDepth      = MinDitchDepth,
                ArrowStep          = ArrowStep,
                GlyphScale         = GlyphScale,
                TextHeight         = TextHeight,
                ShowPointLabels    = ShowPointLabels,
                ShowGradeLabels    = ShowGradeLabels,
                BasinZone          = BasinZone,
                BasinZoneOpacity   = BasinZoneOpacity,
                BasinBandWidth     = BasinBandWidth
            };
        }
    }
}
