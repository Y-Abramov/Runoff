using System;
using System.Collections.Generic;

namespace AbrRunoff.Core
{
    /// <summary>Труба в терминах ядра. Мост конвертирует сюда Topomatic.Alg.Pipes.Pipe.</summary>
    public struct PipeRef
    {
        public double Station;
        public double Diameter;
    }

    /// <summary>
    /// Накладывает нормы на готовый результат детектора. Отдельно от FlowDetector
    /// сознательно: норму меняют (обновят СП, юзер выставит своё), детектор при
    /// этом трогать не нужно.
    /// </summary>
    public static class RunoffChecks
    {
        public static void Apply(RunoffResult result, RunoffSettings settings, IList<PipeRef> pipes)
        {
            if (result == null) return;
            settings = settings ?? new RunoffSettings();

            foreach (var seg in result.Segments)
            {
                // Плато уже помечено ZeroGrade - второй ярлык BelowNorm на нём
                // только зашумил бы ведомость: причина и так названа точнее.
                if ((seg.Flags & SegmentFlags.ZeroGrade) != 0) continue;

                if (seg.MinGradePermille < settings.MinGradePermille)
                    seg.Flags |= SegmentFlags.BelowNorm;
            }

            foreach (var pt in result.Points)
            {
                if (pt.Kind != PointKind.Collector) continue;

                PipeRef? nearest = null;
                double bestDist = double.MaxValue;
                if (pipes != null)
                {
                    foreach (var pipe in pipes)
                    {
                        double dist = Math.Abs(pipe.Station - pt.Station);
                        if (dist > settings.PipeTolerance || dist >= bestDist) continue;
                        bestDist = dist;
                        nearest = pipe;
                    }
                }

                if (nearest.HasValue)
                {
                    pt.PipeStation = nearest.Value.Station;
                    pt.PipeDiameter = nearest.Value.Diameter;
                    pt.Problem = ProblemKind.None;
                }
                else
                {
                    pt.Problem = ProblemKind.NoOutlet;
                }
            }
        }
    }
}
