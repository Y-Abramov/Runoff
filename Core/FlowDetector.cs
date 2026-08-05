using System;
using System.Collections.Generic;

namespace AbrRunoff.Core
{
    /// <summary>
    /// Разбивает ряд отсчётов на участки с одним направлением стока и находит
    /// водоразделы, точки сбора и выходы за пределы диапазона.
    /// Норм не знает - проверки живут в RunoffChecks.
    /// </summary>
    public static class FlowDetector
    {
        public static RunoffResult Detect(RunoffInput input)
        {
            var result = new RunoffResult();
            if (input == null) return result;

            DetectSide(input.Left, DitchSide.Left, input.Settings, result);
            DetectSide(input.Right, DitchSide.Right, input.Settings, result);
            return result;
        }

        private static void DetectSide(IList<DitchSample> samples, DitchSide side,
                                       RunoffSettings settings, RunoffResult result)
        {
            if (samples == null) return;
            foreach (var range in SplitRanges(samples))
                DetectRange(range, side, settings, result);
        }

        /// <summary>
        /// Режет ряд на непрерывные диапазоны кювета. Пикет без кювета - разрыв,
        /// сток через него не идёт (это физически разные канавы).
        /// </summary>
        private static IEnumerable<List<DitchSample>> SplitRanges(IList<DitchSample> samples)
        {
            var current = new List<DitchSample>();
            for (int i = 0; i < samples.Count; i++)
            {
                if (samples[i].IsDitch)
                {
                    current.Add(samples[i]);
                }
                else if (current.Count > 0)
                {
                    yield return current;
                    current = new List<DitchSample>();
                }
            }
            if (current.Count > 0) yield return current;
        }

        private static void DetectRange(List<DitchSample> range, DitchSide side,
                                        RunoffSettings settings, RunoffResult result)
        {
            if (range.Count < 2) return;

            double eps = settings.PlateauEpsPermille / 1000.0;

            // 1. Направление каждого межточечного интервала.
            var dirs = new FlowDirection[range.Count - 1];
            var grades = new double[range.Count - 1];
            for (int i = 0; i < range.Count - 1; i++)
            {
                double ds = range[i + 1].Station - range[i].Station;
                double grade = ds > 1e-9 ? (range[i + 1].BottomZ - range[i].BottomZ) / ds : 0.0;
                grades[i] = grade;

                // отметка падает с ростом пикетажа -> вода идёт вперёд
                if (grade < -eps) dirs[i] = FlowDirection.Forward;
                else if (grade > eps) dirs[i] = FlowDirection.Backward;
                else dirs[i] = FlowDirection.None;
            }

            // 2. Склейка соседних интервалов с одинаковым направлением в участки.
            var segments = new List<FlowSegment>();
            int start = 0;
            for (int i = 1; i <= dirs.Length; i++)
            {
                if (i < dirs.Length && dirs[i] == dirs[start]) continue;

                double sum = 0.0, min = double.MaxValue;
                for (int k = start; k < i; k++)
                {
                    double abs = Math.Abs(grades[k]) * 1000.0;   // в промилле
                    sum += abs;
                    if (abs < min) min = abs;
                }

                var seg = new FlowSegment
                {
                    Side             = side,
                    StationFrom      = range[start].Station,
                    StationTo        = range[i].Station,
                    Direction        = dirs[start],
                    AvgGradePermille = sum / (i - start),
                    MinGradePermille = min,
                    Flags            = dirs[start] == FlowDirection.None
                                       ? SegmentFlags.ZeroGrade : SegmentFlags.None
                };
                segments.Add(seg);
                start = i;
            }

            result.Segments.AddRange(segments);

            // 3. Экстремумы на стыках участков.
            //    Forward -> Backward: вода приходит с обеих сторон = точка сбора (минимум).
            //    Backward -> Forward: вода уходит в обе стороны = водораздел (максимум).
            //    Стык с плато экстремумом не считается.
            for (int i = 0; i + 1 < segments.Count; i++)
            {
                var a = segments[i].Direction;
                var b = segments[i + 1].Direction;
                PointKind kind;
                if (a == FlowDirection.Forward && b == FlowDirection.Backward) kind = PointKind.Collector;
                else if (a == FlowDirection.Backward && b == FlowDirection.Forward) kind = PointKind.Watershed;
                else continue;

                double station = segments[i].StationTo;
                result.Points.Add(new FlowPoint
                {
                    Side    = side,
                    Station = station,
                    BottomZ = ZAt(range, station),
                    Kind    = kind
                });
            }

            // 4. Концы диапазона - выход стока наружу, но только если вода туда течёт.
            if (segments[0].Direction == FlowDirection.Backward)
            {
                result.Points.Add(new FlowPoint
                {
                    Side    = side,
                    Station = range[0].Station,
                    BottomZ = range[0].BottomZ,
                    Kind    = PointKind.Outlet
                });
            }
            var last = segments[segments.Count - 1];
            if (last.Direction == FlowDirection.Forward)
            {
                result.Points.Add(new FlowPoint
                {
                    Side    = side,
                    Station = range[range.Count - 1].Station,
                    BottomZ = range[range.Count - 1].BottomZ,
                    Kind    = PointKind.Outlet
                });
            }
        }

        private static double ZAt(List<DitchSample> range, double station)
        {
            for (int i = 0; i < range.Count; i++)
                if (Math.Abs(range[i].Station - station) < 1e-6) return range[i].BottomZ;
            return 0.0;
        }
    }
}
