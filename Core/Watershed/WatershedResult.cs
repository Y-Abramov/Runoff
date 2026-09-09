using System.Collections.Generic;

namespace AbrRunoff.Core.Watershed
{
    /// <summary>Готовый результат по одному замыкающему створу.</summary>
    public sealed class WatershedResult
    {
        public string OutletName = "";
        public Pt OutletPoint;          // после привязки
        public Pt RequestedPoint;       // как просил пользователь
        public double SnapShiftM;

        public double AreaM2;
        public double AreaKm2 { get { return AreaM2 / 1e6; } }
        public double LengthM;
        public double EndSlope;
        public double WeightedSlope;
        public double HeadZ;
        public double OutletZ;
        public double MeanBasinSlope;

        public List<List<Pt>> Contour = new List<List<Pt>>();
        public List<Pt> Path = new List<Pt>();

        /// <summary>Отметки вдоль лога от створа вверх - для профиля и уклонов.</summary>
        public List<double> PathElevations = new List<double>();

        public List<string> Statuses = new List<string>();
        public bool HasProblem { get { return Statuses.Count > 0; } }
    }
}
