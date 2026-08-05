using System.Collections;
using Topomatic.Cad.Foundation;
using Topomatic.Dwg;
using Topomatic.Dwg.Entities;

namespace AbrRunoff.Entity
{
    public class DrainageNetworkController : DwgEntityController
    {
        protected override void OnPaintEntity(DwgEntity entity, PaintEntityEventArgs args)
        {
            try
            {
                var e = (DwgDrainageNetwork)entity;
                var block = e.GetPlanBlock(IsDetailZoom(args));
                if (block != null && block.Count > 0)
                    PaintEntityEventArgs.PaintEntities(e, block, Vector3D.Empty, Vector3D.One, 0.0, args);
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
                return width <= 0.0 || width < 3000.0;
            }
            catch { return true; }
        }

        public override IEnumerable GetGrips(DwgEntity entity, object cadview)
        {
            // Сеть - производный объект: геометрия задаётся источниками.
            yield break;
        }
    }
}
