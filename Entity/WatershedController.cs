using System.Collections;
using Topomatic.Cad.Foundation;
using Topomatic.Dwg;
using Topomatic.Dwg.Entities;

namespace AbrRunoff.Entity
{
    public class WatershedController : DwgEntityController
    {
        protected override void OnPaintEntity(DwgEntity entity, PaintEntityEventArgs args)
        {
            try
            {
                var e = (DwgWatershed)entity;
                var block = e.GetPlanBlock(IsDetailZoom(args));
                if (block != null && block.Count > 0)
                {
                    // блок уже в мировых координатах
                    PaintEntityEventArgs.PaintEntities(e, block, Vector3D.Empty, Vector3D.One, 0.0, args);
                }
            }
            catch
            {
                // исключение в paint = краш Robur; молча пропускаем кадр
            }
        }

        private static bool IsDetailZoom(PaintEntityEventArgs args)
        {
            try
            {
                double width = args.Pen.ViewBounds.Width;
                return width <= 0.0 || width < RunoffSchemeController.DetailScaleLimit;
            }
            catch { return true; }
        }

        public override IEnumerable GetGrips(DwgEntity entity, object cadview)
        {
            // Геометрия водосбора - снапшот прошлого расчёта, не живая проекция
            // источника: тащить на плане нечего, грипов нет.
            return new object[0];
        }
    }
}
