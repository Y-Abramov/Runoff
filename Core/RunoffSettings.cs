namespace AbrRunoff.Core
{
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

        /// <summary>Подложка под подписями - без неё текст тонет в линиях плана.</summary>
        public bool LabelBackground = true;

        /// <summary>Подписи характерных точек (водораздел, сбор, сброс).</summary>
        public bool ShowPointLabels = true;

        /// <summary>Подписи уклона вдоль участков.</summary>
        public bool ShowGradeLabels = true;

        public RunoffSettings Clone()
        {
            return new RunoffSettings
            {
                MinGradePermille   = MinGradePermille,
                PlateauEpsPermille = PlateauEpsPermille,
                PipeTolerance      = PipeTolerance,
                SampleStep         = SampleStep,
                ArrowStep          = ArrowStep,
                GlyphScale         = GlyphScale,
                TextHeight         = TextHeight,
                LabelBackground    = LabelBackground,
                ShowPointLabels    = ShowPointLabels,
                ShowGradeLabels    = ShowGradeLabels
            };
        }
    }
}
