using System;
using System.Collections.Generic;
using AbrRunoff.Core.Watershed;

namespace AbrRunoff.Robur
{
    /// <summary>Один запрошенный замыкающий створ: имя (трубы или ручной точки) + мировая точка.</summary>
    internal sealed class WatershedRequest
    {
        public string OutletName = "";
        public double X, Y;
    }

    /// <summary>Рецепт расчёта + его результат - всё, что нужно, чтобы наполнить DwgWatershed.</summary>
    internal sealed class WatershedBuildResult
    {
        public string SurfaceName = "";
        public string DesignSurfaceName = "";
        public string OutletName = "";
        public double OutletX, OutletY;
        public double Step;
        public WatershedSettings Settings;
        public string SourceHash = "";

        /// <summary>Null при отказе - см. Error.</summary>
        public WatershedResult Result;
        public string Error;
    }

    /// <summary>
    /// Мост между диалогом/командой и ядром: растеризация выбранной поверхности
    /// один раз, врезка проектной, подготовка сетки один раз, расчёт по каждому
    /// створу на ОДНОЙ подготовленной сетке. Общий код для первого построения
    /// (несколько створов сразу) и пересчёта одного устаревшего водосбора.
    /// </summary>
    internal static class WatershedBuilder
    {
        internal static List<WatershedBuildResult> Build(SurfaceRef surface, SurfaceRef design,
            double step, WatershedSettings settings, IList<WatershedRequest> outlets,
            Action<int, int, string> progress)
        {
            var effective = EffectiveSettings(settings, step);

            var list = new List<WatershedBuildResult>();
            if (progress != null) progress(0, outlets.Count + 1, "Растеризация поверхности");

            var grid = SurfaceReader.BuildGrid(surface, step);
            if (design != null) SurfaceReader.BurnDesign(grid, design);

            var prepared = WatershedSolver.Prepare(grid, effective);

            for (int i = 0; i < outlets.Count; i++)
            {
                var o = outlets[i];
                if (progress != null) progress(i + 1, outlets.Count + 1, "Расчёт: " + o.OutletName);

                string hash = SurfaceReader.ComputeSourceHash(surface, design, o.X, o.Y, step);
                string error;
                var res = WatershedSolver.Solve(prepared, new Pt(o.X, o.Y), o.OutletName, effective, out error);

                list.Add(new WatershedBuildResult
                {
                    SurfaceName = surface.Name,
                    DesignSurfaceName = design == null ? "" : design.Name,
                    OutletName = o.OutletName,
                    OutletX = o.X,
                    OutletY = o.Y,
                    Step = step,
                    Settings = effective,
                    SourceHash = hash,
                    Result = res,
                    Error = error
                });
            }
            return list;
        }

        /// <summary>
        /// Радиус привязки створа к тальвегу обязан покрывать хотя бы пару ячеек
        /// сетки - иначе PourPoint.TrySnap ищет фактически внутри одной ячейки
        /// (радиус меньше шага почти не расширяет поиск за её пределы) и почти
        /// любой клик мимо готовой клетки-канала отказывает с «водотока не найдено»,
        /// даже если клик лёг точно на видимый тальвег. Настройки не трогаем -
        /// возвращаем копию только когда исходный радиус этого не покрывает.
        /// </summary>
        private static WatershedSettings EffectiveSettings(WatershedSettings s, double step)
        {
            double minRadius = step * 2.0;
            if (s.SnapRadius >= minRadius) return s;

            return new WatershedSettings
            {
                MaxCells = s.MaxCells,
                SnapRadius = minRadius,
                MinChannelCells = s.MinChannelCells,
                FillEpsilon = s.FillEpsilon,
                HoleFillMaxCells = s.HoleFillMaxCells,
                DeepFillWarn = s.DeepFillWarn,
                AreaTolerancePercent = s.AreaTolerancePercent,
                SimplifyToleranceCells = s.SimplifyToleranceCells
            };
        }
    }
}
