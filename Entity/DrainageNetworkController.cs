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
            // Сама сеть за грипы не тянется (геометрия задаётся источниками) -
            // грипы висят только на подписях выпусков и тупиков.
            var e = entity as DwgDrainageNetwork;
            if (e == null) return new object[0];

            return LabelGrips.Build(entity, cadview, e.PlacedLabels,
                                    e.SetLabelOffset, e.ResetLabelOffset);
        }
    }
}
