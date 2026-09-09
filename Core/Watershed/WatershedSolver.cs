using System;
using System.Collections.Generic;
using System.Globalization;

namespace AbrRunoff.Core.Watershed
{
    /// <summary>Сетка, прошедшая подготовку. Считается один раз на все створы.</summary>
    public sealed class PreparedGrid
    {
        public Grid Grid;
        public int[] Dir;
        public int[] Acc;
        public FillReport Fill;
        public int FilledHoles;
    }

    public static class WatershedSolver
    {
        /// <summary>
        /// Тяжёлая часть конвейера. Не зависит от створа, поэтому пакетный прогон
        /// по всем трубам объекта считает её один раз.
        /// </summary>
        public static PreparedGrid Prepare(Grid g, WatershedSettings s)
        {
            var prepared = new PreparedGrid { Grid = g };
            prepared.FilledHoles = HoleFiller.FillSmallHoles(g, s.HoleFillMaxCells);
            prepared.Fill = DepressionFill.Fill(g, s.FillEpsilon);
            prepared.Dir = FlowDirection.Compute(g);
            prepared.Acc = FlowAccumulation.Compute(g, prepared.Dir);
            return prepared;
        }

        public static WatershedResult Solve(PreparedGrid p, Pt requested, string outletName,
                                            WatershedSettings s, out string error)
        {
            error = null;
            var g = p.Grid;

            int outlet;
            double shift;
            if (!PourPoint.TrySnap(g, p.Acc, requested.X, requested.Y, s, out outlet, out shift))
            {
                error = "Водотока рядом со створом не найдено: в радиусе " +
                        s.SnapRadius.ToString("0.#", CultureInfo.CurrentCulture) +
                        " м нет ячейки с водосбором от " + s.MinChannelCells + " ячеек.";
                return null;
            }

            var mask = WatershedTracer.Trace(g, p.Dir, outlet);
            var rings = BoundaryTrace.TraceRings(g, mask);
            var contour = Simplify.FitTolerance(rings, mask.AreaM2, g.Step,
                                                s.SimplifyToleranceCells, s.AreaTolerancePercent);
            var pathCells = MainPath.Compute(g, p.Dir, mask, outlet);

            var res = new WatershedResult
            {
                OutletName = outletName ?? "",
                RequestedPoint = requested,
                OutletPoint = g.Center(outlet),
                SnapShiftM = shift,
                AreaM2 = Simplify.PolygonArea(contour),
                Contour = contour,
                LengthM = Metrics.PathLength(g, pathCells),
                OutletZ = g.Z[outlet],
                HeadZ = g.Z[pathCells[0]],
                MeanBasinSlope = Metrics.MeanBasinSlope(g, mask)
            };

            foreach (int cell in pathCells) res.Path.Add(g.Center(cell));

            // Отметки и длины участков от створа вверх - для средневзвешенного уклона.
            var elevations = new List<double>();
            var segments = new List<double>();
            for (int k = pathCells.Count - 1; k >= 0; k--)
            {
                elevations.Add(g.Z[pathCells[k]]);
                if (k < pathCells.Count - 1)
                {
                    var a = g.Center(pathCells[k + 1]);
                    var b = g.Center(pathCells[k]);
                    double dx = b.X - a.X, dy = b.Y - a.Y;
                    segments.Add(Math.Sqrt(dx * dx + dy * dy));
                }
            }
            res.PathElevations = elevations;
            res.EndSlope = Metrics.EndSlope(res.HeadZ, res.OutletZ, res.LengthM);
            res.WeightedSlope = Metrics.WeightedSlope(elevations, segments);

            if (mask.TouchesEdge)
                res.Statuses.Add("Водосбор упёрся в край поверхности - площадь занижена, нужен рельеф большего охвата");

            if (shift > g.Step * 0.5)
                res.Statuses.Add("Привязка створа сдвинута на " + shift.ToString("0.#", CultureInfo.CurrentCulture) + " м");

            if (p.Fill != null && p.Fill.MaxDepth > s.DeepFillWarn)
                res.Statuses.Add("Заполнена впадина глубиной " +
                                 p.Fill.MaxDepth.ToString("0.#", CultureInfo.CurrentCulture) +
                                 " м - проверьте рельеф на месте");

            return res;
        }

        public static List<WatershedResult> SolveMany(PreparedGrid p,
                                                      IList<KeyValuePair<string, Pt>> outlets,
                                                      WatershedSettings s)
        {
            var list = new List<WatershedResult>();
            foreach (var o in outlets)
            {
                string error;
                var res = Solve(p, o.Value, o.Key, s, out error);
                if (res != null) list.Add(res);
            }
            return list;
        }
    }
}
