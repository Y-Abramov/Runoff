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
            var list = new List<WatershedBuildResult>();
            if (progress != null) progress(0, outlets.Count + 1, "Растеризация поверхности");

            var grid = SurfaceReader.BuildGrid(surface, step);
            if (design != null) SurfaceReader.BurnDesign(grid, design);

            var prepared = WatershedSolver.Prepare(grid, settings);

            for (int i = 0; i < outlets.Count; i++)
            {
                var o = outlets[i];
                if (progress != null) progress(i + 1, outlets.Count + 1, "Расчёт: " + o.OutletName);

                string hash = SurfaceReader.ComputeSourceHash(surface, design, o.X, o.Y, step);
                string error;
                var res = WatershedSolver.Solve(prepared, new Pt(o.X, o.Y), o.OutletName, settings, out error);

                list.Add(new WatershedBuildResult
                {
                    SurfaceName = surface.Name,
                    DesignSurfaceName = design == null ? "" : design.Name,
                    OutletName = o.OutletName,
                    OutletX = o.X,
                    OutletY = o.Y,
                    Step = step,
                    Settings = settings,
                    SourceHash = hash,
                    Result = res,
                    Error = error
                });
            }
            return list;
        }
    }
}
