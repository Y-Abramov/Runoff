using System.Collections.Generic;

namespace AbrRunoff.Core.Network
{
    /// <summary>
    /// Относит каждый узел к бассейну его конечного выпуска.
    ///
    /// Номер бассейна берётся из позиции выпуска в списке, ОТСОРТИРОВАННОМ по
    /// координате, а не из порядка обхода. Иначе перестроение схемы перекрашивало бы
    /// бассейны заново, и проектировщик терял бы ориентацию между итерациями -
    /// цвет на плане должен означать одно и то же от прогона к прогону.
    /// </summary>
    public static class BasinAssigner
    {
        /// <summary>Узел -> номер бассейна. -1 если сток никуда не приходит.</summary>
        public static Dictionary<int, int> Assign(DrainageNetwork net)
        {
            var outfallOf = new Dictionary<int, int>();
            var outfalls = new List<DrainageNode>();

            foreach (var node in net.Nodes)
            {
                var trace = net.Trace(node.Id);
                int target = trace.Outcome == TraceOutcome.ReachedOutfall ? trace.OutfallNode : -1;
                outfallOf[node.Id] = target;
            }

            foreach (var node in net.Nodes)
                if (node.Outfall != OutfallKind.None) outfalls.Add(node);

            // Устойчивый порядок: сначала по X, потом по Y, потом по Id.
            outfalls.Sort(delegate (DrainageNode a, DrainageNode b)
            {
                int byX = a.X.CompareTo(b.X);
                if (byX != 0) return byX;
                int byY = a.Y.CompareTo(b.Y);
                if (byY != 0) return byY;
                return a.Id.CompareTo(b.Id);
            });

            var basinOfOutfall = new Dictionary<int, int>(outfalls.Count);
            for (int i = 0; i < outfalls.Count; i++) basinOfOutfall[outfalls[i].Id] = i;

            var result = new Dictionary<int, int>(net.Nodes.Count);
            foreach (var kv in outfallOf)
            {
                int basin;
                result[kv.Key] = kv.Value >= 0 && basinOfOutfall.TryGetValue(kv.Value, out basin) ? basin : -1;
            }
            return result;
        }
    }
}
