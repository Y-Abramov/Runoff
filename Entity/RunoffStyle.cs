using Topomatic.Cad.Foundation;
using Topomatic.Dwg;

namespace AbrRunoff.Entity
{
    /// <summary>
    /// Палитра и метрики схемы стока в одном месте: цвет и толщина задаются
    /// смыслом («норма», «предупреждение», «ошибка»), а не подбираются по месту.
    /// Правка стиля не требует лазить в код построения геометрии.
    /// </summary>
    internal static class RunoffStyle
    {
        // Канон ABR: акцент #0891B2. Янтарь - предупреждение, красный - ошибка.
        internal static readonly CadColor Normal  = Rgb(0x08, 0x91, 0xB2);
        internal static readonly CadColor Warning = Rgb(0xE8, 0xA0, 0x00);
        internal static readonly CadColor Error   = Rgb(0xD9, 0x3A, 0x2B);
        internal static readonly CadColor Muted   = Rgb(0x7A, 0x8A, 0x93);
        internal static readonly CadColor Paper   = Rgb(0xFF, 0xFF, 0xFF);

        /// <summary>Линия дна: заметнее подложки плана, но тоньше проектных линий.</summary>
        internal const Lineweight LineMain  = Lineweight.lw035;
        internal const Lineweight LineThin  = Lineweight.lw018;
        internal const Lineweight LineHeavy = Lineweight.lw050;

        /// <summary>Базовый размер знака в метрах плана до применения GlyphScale.</summary>
        internal const double GlyphBase = 2.4;

        /// <summary>Отступ подписи от знака, в долях размера знака.</summary>
        internal const double LabelGap = 1.4;

        /// <summary>Ширина символа относительно высоты - для оценки габарита подписи.</summary>
        internal const double CharWidthRatio = 0.62;

        /// <summary>
        /// DwgText.Rotation принимает ГРАДУСЫ - подтверждено живым тестом 2026-08-05.
        /// Справочник Robur единицы для DwgText не называет, а косвенная улика вела
        /// не туда: Vertex.Beta документирован «в радианах», но у DwgText соглашение
        /// другое. Единицы углов с одного типа Robur на другой не экстраполировать.
        /// </summary>
        internal const bool TextRotationInDegrees = true;

        /// <summary>Приводит угол из радиан в те единицы, которых ждёт DwgText.Rotation.</summary>
        internal static double TextAngle(double radians)
        {
            return TextRotationInDegrees ? radians * 180.0 / System.Math.PI : radians;
        }

        private static CadColor Rgb(int r, int g, int b)
        {
            return new CadColor(System.Drawing.Color.FromArgb(r, g, b));
        }
    }
}
