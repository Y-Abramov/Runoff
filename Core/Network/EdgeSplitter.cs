using System;

namespace AbrRunoff.Core.Network
{
    /// <summary>
    /// Врезка точки примыкания в существующее ребро сети.
    ///
    /// ЗАЧЕМ. Труба стоит там, где её поставил проектировщик, - как правило, в
    /// середине перегона, где готового узла нет: узлы ставятся только в
    /// характерных точках (водоразделы, точки сбора, концы). Прежняя логика
    /// искала ГОТОВЫЙ узел в допуске и, не найдя, молча выбрасывала трубу из
    /// сети. Правильно - разрезать ребро в точке примыкания.
    /// </summary>
    public static class EdgeSplitter
    {
        /// <summary>
        /// Доля длины ребра, ближе которой к его концу врезка не делается, а
        /// переиспользуется существующий узел. Иначе плодились бы рёбра почти
        /// нулевой длины с бессмысленным уклоном.
        /// </summary>
        private const double SnapFraction = 0.02;

        /// <summary>Минимальная длина огрызка, м - страховка от коротких рёбер.</summary>
        private const double SnapMin = 0.5;

        /// <summary>
        /// Находит на рёбрах сети ближайшую к (x, y) точку и врезает туда узел.
        /// Возвращает Id узла примыкания или -1, если ничего ближе tolerance нет.
        /// </summary>
        public static int AttachAt(DrainageNetwork net, double x, double y, double tolerance,
                                   ref int nextNodeId)
        {
            if (net == null || net.Edges.Count == 0) return -1;

            DrainageEdge best = null;
            double bestDist = tolerance;
            double bestT = 0.0;

            foreach (var e in net.Edges)
            {
                var a = net.NodeById(e.FromNode);
                var b = net.NodeById(e.ToNode);
                if (a == null || b == null) continue;

                double t;
                double d = DistanceToSegment(x, y, a.X, a.Y, b.X, b.Y, out t);
                if (d > bestDist) continue;

                bestDist = d; best = e; bestT = t;
            }

            if (best == null) return -1;

            var from = net.NodeById(best.FromNode);
            var to = net.NodeById(best.ToNode);
            double length = best.Length;

            // Рядом с готовым узлом врезку не делаем - берём его.
            double snap = Math.Max(length * SnapFraction, SnapMin);
            if (bestT * length <= snap) return best.FromNode;
            if ((1.0 - bestT) * length <= snap) return best.ToNode;

            var mid = new DrainageNode
            {
                Id = nextNodeId++,
                X = from.X + (to.X - from.X) * bestT,
                Y = from.Y + (to.Y - from.Y) * bestT,
                Z = from.Z + (to.Z - from.Z) * bestT,
                Kind = NodeKind.PipeJunction,
                SourceRef = best.SourceRef
            };
            net.Nodes.Add(mid);

            // Ребро заменяется двумя: направление течения сохраняется, метаданные
            // (достоверность, источник, уклон) наследуются - разрез не меняет того,
            // откуда взялась связь.
            int oldTo = best.ToNode;
            double oldLength = best.Length;

            best.ToNode = mid.Id;
            best.Length = oldLength * bestT;

            net.Edges.Add(new DrainageEdge
            {
                Id = NextEdgeId(net),
                FromNode = mid.Id,
                ToNode = oldTo,
                Length = oldLength * (1.0 - bestT),
                GradePermille = best.GradePermille,
                Confidence = best.Confidence,
                Element = best.Element,
                SourceRef = best.SourceRef
            });

            net.InvalidateIndex();
            return mid.Id;
        }

        private static int NextEdgeId(DrainageNetwork net)
        {
            int max = 0;
            foreach (var e in net.Edges) if (e.Id > max) max = e.Id;
            return max + 1;
        }

        /// <summary>Расстояние от точки до отрезка; t - доля проекции вдоль отрезка (0..1).</summary>
        private static double DistanceToSegment(double px, double py,
                                                double ax, double ay, double bx, double by,
                                                out double t)
        {
            double dx = bx - ax, dy = by - ay;
            double len2 = dx * dx + dy * dy;

            if (len2 < 1e-18) { t = 0.0; return Math.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay)); }

            t = ((px - ax) * dx + (py - ay) * dy) / len2;
            if (t < 0.0) t = 0.0;
            if (t > 1.0) t = 1.0;

            double cx = ax + dx * t, cy = ay + dy * t;
            return Math.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
        }
    }
}
