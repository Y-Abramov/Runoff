using Topomatic.Cad.Foundation;

namespace AbrRunoff.Entity
{
    /// <summary>
    /// Качественная палитра бассейнов - различимые оттенки, не градиент:
    /// номер бассейна не означает «больше/меньше», он означает «другой».
    /// Цвета подобраны различимыми при печати в оттенках серого.
    /// </summary>
    internal static class BasinPalette
    {
        /// <summary>
        /// КРАСНОГО ЗДЕСЬ НЕТ СОЗНАТЕЛЬНО. Цвет на схеме говорит ровно одно:
        /// «принадлежит такому-то бассейну». Проблема говорится ФОРМОЙ (двойное
        /// кольцо, жирная обводка) и только красным. Пока в палитре был 0xD62728,
        /// бассейн №4 выглядел как ошибка - два языка спорили за один канал.
        /// </summary>
        private static readonly int[] s_Argb =
        {
            0x1F77B4, 0xFF7F0E, 0x2CA02C, 0x9467BD, 0x17BECF,
            0x8C564B, 0xE377C2, 0xBCBD22, 0x4C72B0, 0x937860
        };

        internal static CadColor For(int basinId)
        {
            if (basinId < 0) return RunoffStyle.Muted;   // сток никуда не приходит
            int rgb = s_Argb[basinId % s_Argb.Length];
            return new CadColor(System.Drawing.Color.FromArgb(
                (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF));
        }

        /// <summary>
        /// Угол штриховки зоны бассейна. Запасной язык различения, когда заливка
        /// прозрачностью не читается: соседние бассейны получают разный наклон.
        /// Шаг 30° и простые числа в индексе - чтобы соседние номера не совпали.
        /// </summary>
        internal static double HatchAngle(int basinId)
        {
            if (basinId < 0) return 45.0;
            return (basinId * 37) % 180;
        }

        internal static int Count { get { return s_Argb.Length; } }
    }
}
