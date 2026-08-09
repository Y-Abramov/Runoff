using System.Collections.Generic;

namespace AbrRunoff.Core.Network
{
    /// <summary>Отрезок сети в плане - звено ленты бассейна.</summary>
    public struct BasinSegment
    {
        public double X1, Y1, X2, Y2;
        public BasinSegment(double x1, double y1, double x2, double y2)
        {
            X1 = x1; Y1 = y1; X2 = x2; Y2 = y2;
        }
    }

    /// <summary>Бассейн целиком: и для легенды-таблицы, и для подсветки на плане.</summary>
    public sealed class BasinInfo
    {
        /// <summary>Номер для человека, с 1. Совпадает с BasinAssigner + 1.</summary>
        public int Number;

        /// <summary>Внутренний номер BasinAssigner (с 0) - для палитры.</summary>
        public int Index;

        public bool HasOutfall;
        public int OutfallNodeId = -1;
        public OutfallKind OutfallKind = OutfallKind.None;
        public double OutfallZ;

        /// <summary>Суммарная длина рёбер бассейна, м.</summary>
        public double TotalLength;

        public int NodeCount;

        /// <summary>Бассейн без выпуска - вода собирается и никуда не уходит.</summary>
        public bool IsProblem { get { return !HasOutfall; } }

        /// <summary>
        /// Звенья сети этого бассейна. Подсветка идёт ЛЕНТОЙ вдоль них, а не
        /// заливкой площади: сеть водоотвода линейна и ветвиста, любая огибающая
        /// (пробовали выпуклую оболочку - 2026-08-09) превращается в кляксу на
        /// пол-чертежа и накрывает то, к чему бассейн отношения не имеет.
        /// </summary>
        public List<BasinSegment> Segments = new List<BasinSegment>();
    }

    /// <summary>
    /// Сводка по бассейнам. Отвечает на вопрос, ради которого смотрят схему:
    /// сколько бассейнов, куда каждый сбрасывает и где беда.
    ///
    /// Без Topomatic - считается на графе и проверяется тестами без Robur.
    /// </summary>
    public static class BasinSummary
    {
        public static List<BasinInfo> Build(DrainageNetwork net, Dictionary<int, int> basins)
        {
            var result = new List<BasinInfo>();
            if (net == null || basins == null) return result;

            var byId = new Dictionary<int, DrainageNode>(net.Nodes.Count);
            foreach (var n in net.Nodes) byId[n.Id] = n;

            var byIndex = new Dictionary<int, BasinInfo>();

            foreach (var node in net.Nodes)
            {
                int index;
                if (!basins.TryGetValue(node.Id, out index)) continue;
                if (index < 0) continue;   // сток никуда не приходит - разбирается ниже

                BasinInfo info;
                if (!byIndex.TryGetValue(index, out info))
                {
                    info = new BasinInfo { Index = index, Number = index + 1 };
                    byIndex[index] = info;
                    result.Add(info);
                }

                info.NodeCount++;

                if (node.Outfall != OutfallKind.None && !info.HasOutfall)
                {
                    info.HasOutfall = true;
                    info.OutfallNodeId = node.Id;
                    info.OutfallKind = node.Outfall;
                    info.OutfallZ = node.Z;
                }
            }

            // Длина и лента - по рёбрам, а не по узлам: узел может принадлежать
            // бассейну, а ребро из него уводить в другой (развилка на два выпуска).
            foreach (var e in net.Edges)
            {
                int index;
                if (!basins.TryGetValue(e.FromNode, out index)) continue;
                if (index < 0) continue;

                BasinInfo info;
                if (!byIndex.TryGetValue(index, out info)) continue;

                info.TotalLength += e.Length;
                AddSegment(info, byId, e);
            }

            result.Sort(delegate (BasinInfo a, BasinInfo b) { return a.Number.CompareTo(b.Number); });

            // Бассейны БЕЗ выпуска. BasinAssigner валит все такие узлы в один -1,
            // но это не один бассейн: две разные тупиковые зоны - две разные
            // проблемы, и в сводке они обязаны быть разными строками.
            AddDeadEndBasins(net, basins, byId, result);
            return result;
        }

        private static void AddSegment(BasinInfo info, Dictionary<int, DrainageNode> byId, DrainageEdge e)
        {
            DrainageNode a, b;
            if (!byId.TryGetValue(e.FromNode, out a)) return;
            if (!byId.TryGetValue(e.ToNode, out b)) return;
            info.Segments.Add(new BasinSegment(a.X, a.Y, b.X, b.Y));
        }

        /// <summary>
        /// Тупиковые зоны как отдельные бассейны. Компонента без единого ребра -
        /// это одинокий узел, а не зона водосбора: такой шум пропускаем, иначе
        /// каждая висячая точка порождала бы строку в ведомости.
        /// </summary>
        private static void AddDeadEndBasins(DrainageNetwork net, Dictionary<int, int> basins,
                                             Dictionary<int, DrainageNode> byId, List<BasinInfo> result)
        {
            var orphans = new Dictionary<int, DrainageNode>();
            foreach (var node in net.Nodes)
            {
                int index;
                if (!basins.TryGetValue(node.Id, out index) || index < 0) orphans[node.Id] = node;
            }
            if (orphans.Count == 0) return;

            // Связи внутри тупиковой зоны - без учёта направления: зона едина,
            // даже если вода внутри неё сходится с двух сторон.
            var neighbours = new Dictionary<int, List<int>>();
            var hasEdge = new HashSet<int>();
            foreach (var e in net.Edges)
            {
                if (!orphans.ContainsKey(e.FromNode) || !orphans.ContainsKey(e.ToNode)) continue;
                Link(neighbours, e.FromNode, e.ToNode);
                Link(neighbours, e.ToNode, e.FromNode);
                hasEdge.Add(e.FromNode);
                hasEdge.Add(e.ToNode);
            }

            var seen = new HashSet<int>();
            var components = new List<List<DrainageNode>>();

            var ids = new List<int>(orphans.Keys);
            ids.Sort();

            foreach (var id in ids)
            {
                if (seen.Contains(id)) continue;

                var stack = new Stack<int>();
                var group = new List<DrainageNode>();
                stack.Push(id);
                seen.Add(id);

                while (stack.Count > 0)
                {
                    int cur = stack.Pop();
                    group.Add(orphans[cur]);

                    List<int> next;
                    if (!neighbours.TryGetValue(cur, out next)) continue;
                    foreach (int n in next)
                        if (seen.Add(n)) stack.Push(n);
                }

                bool any = false;
                foreach (var n in group) if (hasEdge.Contains(n.Id)) { any = true; break; }
                if (any) components.Add(group);
            }

            // Устойчивый порядок: по координате крайнего узла, как у BasinAssigner.
            components.Sort(delegate (List<DrainageNode> a, List<DrainageNode> b)
            {
                var pa = Anchor(a); var pb = Anchor(b);
                int byX = pa.X.CompareTo(pb.X);
                if (byX != 0) return byX;
                return pa.Y.CompareTo(pb.Y);
            });

            int number = result.Count;
            foreach (var group in components)
            {
                number++;
                var info = new BasinInfo
                {
                    Index = -1,          // палитра отдаст приглушённый: это не «ещё один цвет», это беда
                    Number = number,
                    HasOutfall = false,
                    NodeCount = group.Count
                };

                var members = new HashSet<int>();
                foreach (var n in group) members.Add(n.Id);

                foreach (var e in net.Edges)
                {
                    if (!members.Contains(e.FromNode) || !members.Contains(e.ToNode)) continue;
                    info.TotalLength += e.Length;
                    AddSegment(info, byId, e);
                }

                result.Add(info);
            }
        }

        private static void Link(Dictionary<int, List<int>> map, int from, int to)
        {
            List<int> list;
            if (!map.TryGetValue(from, out list)) { list = new List<int>(); map[from] = list; }
            list.Add(to);
        }

        private static DrainageNode Anchor(List<DrainageNode> group)
        {
            var best = group[0];
            foreach (var n in group)
                if (n.X < best.X || (n.X == best.X && n.Y < best.Y)) best = n;
            return best;
        }
    }
}
