using System.Collections.Generic;
using AbrRunoff.Core;
using AbrRunoff.Core.Network;
using Topomatic.Cad.Foundation;

namespace AbrRunoff.Robur.Sources
{
    /// <summary>
    /// Линейный элемент водоотвода в едином виде: ряд отсчётов плюс их координаты
    /// на плане. Ряд идёт в существующий FlowDetector без изменений - ему всё равно,
    /// пикетаж это трассы или накопленная длина структурной линии.
    /// </summary>
    internal sealed class DrainageElement
    {
        public ElementKind Kind;

        /// <summary>Человекочитаемая ссылка для ведомости: имя дороги, линии, лотка.</summary>
        public string SourceRef = "";

        /// <summary>Отсчёты для FlowDetector: Station = расстояние вдоль элемента.</summary>
        public List<DitchSample> Samples = new List<DitchSample>();

        /// <summary>Координата плана для каждого отсчёта, тот же индекс.</summary>
        public List<Vector2D> Positions = new List<Vector2D>();
    }

    /// <summary>
    /// Источник элементов водоотвода. Новый вид водоотвода = один новый класс;
    /// ядро и сшивка при этом не трогаются.
    /// </summary>
    internal interface IDrainageSource
    {
        string Name { get; }
        List<DrainageElement> Read(RunoffSettings settings);
    }
}
