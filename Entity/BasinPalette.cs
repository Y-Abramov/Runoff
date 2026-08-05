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
        private static readonly int[] s_Argb =
        {
            0x1F77B4, 0xFF7F0E, 0x2CA02C, 0xD62728, 0x9467BD,
            0x8C564B, 0xE377C2, 0x7F7F7F, 0xBCBD22, 0x17BECF
        };

        internal static CadColor For(int basinId)
        {
            if (basinId < 0) return RunoffStyle.Muted;   // сток никуда не приходит
            int rgb = s_Argb[basinId % s_Argb.Length];
            return new CadColor(System.Drawing.Color.FromArgb(
                (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF));
        }

        internal static int Count { get { return s_Argb.Length; } }
    }
}
