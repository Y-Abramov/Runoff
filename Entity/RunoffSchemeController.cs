using System;
using System.Collections;
using Topomatic.Cad.Foundation;
using Topomatic.Dwg;
using Topomatic.Dwg.Entities;

namespace AbrRunoff.Entity
{
    public class RunoffSchemeController : DwgEntityController
    {
        /// <summary>
        /// Выше этого масштаба (единиц плана на пиксель) стрелки и подписи не строим:
        /// на 10-километровой трассе это сотни глифов с текстом, отрисовка встаёт.
        /// internal - тот же порог использует WatershedController, второе магическое
        /// число не заводим.
        /// </summary>
        internal const double DetailScaleLimit = 3000.0;

        protected override void OnPaintEntity(DwgEntity entity, PaintEntityEventArgs args)
        {
            try
            {
                var e = (DwgRunoffScheme)entity;
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
                // PaintEntityEventArgs.Bounds не существует - видимая область берётся
                // с пера (тот же приём, что в decompiled Topomatic.Cad.View: pen.ViewBounds).
                double width = args.Pen.ViewBounds.Width;
                return width <= 0.0 || width < DetailScaleLimit;
            }
            catch { return true; }
        }

        public override IEnumerable GetGrips(DwgEntity entity, object cadview)
        {
            // Саму схему за грипы не тянут - её геометрия целиком определяется
            // дорогой. Грипы висят только на подписях: автомат разводит их как
            // может, а на плотном узле последнее слово за проектировщиком.
            var e = entity as DwgRunoffScheme;
            if (e == null) return new object[0];

            return LabelGrips.Build(entity, cadview, e.PlacedLabels,
                                    e.SetLabelOffset, e.ResetLabelOffset);
        }
    }
}
