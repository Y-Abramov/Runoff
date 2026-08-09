using System;
using System.Globalization;

namespace AbrRunoff.Core.Labels
{
    /// <summary>
    /// Устойчивые идентификаторы подписей. Схема пересчитывается заново при каждой
    /// перерисовке, поэтому ручное смещение нельзя привязать ни к индексу в списке,
    /// ни к Id узла (они пересобираются). Ключ строится из САМОЙ ГЕОМЕТРИИ -
    /// сторона, тип, пикет (или координата узла), округлённые до сантиметра.
    ///
    /// Следствие: правка продольного профиля сдвигает подпись и её ручное смещение
    /// теряется. Это правильно - смещение, поставленное под старую геометрию,
    /// после её изменения только вводило бы в заблуждение.
    /// </summary>
    public static class LabelKeys
    {
        /// <summary>Подпись характерной точки: водораздел, точка сбора, сброс.</summary>
        public static string Point(DitchSide side, PointKind kind, double station)
        {
            return string.Concat("P:", SideCode(side), ":", KindCode(kind), ":", Cm(station));
        }

        /// <summary>Подпись уклона участка - привязана к обоим концам участка.</summary>
        public static string Grade(DitchSide side, double stationFrom, double stationTo)
        {
            return string.Concat("G:", SideCode(side), ":", Cm(stationFrom), ":", Cm(stationTo));
        }

        /// <summary>Подпись узла сети - по координате плана, Id узла не устойчив.</summary>
        public static string Node(double x, double y)
        {
            return string.Concat("N:", Cm(x), ":", Cm(y));
        }

        private static string SideCode(DitchSide side)
        {
            return side == DitchSide.Left ? "L" : "R";
        }

        private static string KindCode(PointKind kind)
        {
            switch (kind)
            {
                case PointKind.Watershed: return "W";
                case PointKind.Collector: return "C";
                default:                  return "O";
            }
        }

        /// <summary>
        /// Округление до сантиметра. Гасит копеечный дрейф пересчёта, но различает
        /// реально разные места. Минус в записи безопасен - ключ едет в STG через
        /// разделители '=' и ';', минус в них не входит.
        /// </summary>
        private static string Cm(double meters)
        {
            long cm = (long)Math.Round(meters * 100.0, MidpointRounding.AwayFromZero);
            return cm.ToString(CultureInfo.InvariantCulture);
        }
    }
}
