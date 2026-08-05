using System;
using System.Collections.Generic;

namespace AbrRunoff.Core.Network
{
    /// <summary>
    /// Граф водоотвода. Знает только числа - ни одной ссылки на Topomatic,
    /// поэтому тестируется без установленного Robur.
    /// </summary>
    public sealed class DrainageNetwork
    {
        public List<DrainageNode> Nodes = new List<DrainageNode>();
        public List<DrainageEdge> Edges = new List<DrainageEdge>();

        private Dictionary<int, DrainageNode> m_ById;
        private Dictionary<int, List<DrainageEdge>> m_Out;

        /// <summary>
        /// Индексы строятся лениво и сбрасываются вручную: во время сборки графа
        /// рёбра добавляются пачками, перестраивать индекс на каждое - расточительно.
        /// </summary>
        public void InvalidateIndex()
        {
            m_ById = null;
            m_Out = null;
        }

        private void EnsureIndex()
        {
            if (m_ById != null) return;

            m_ById = new Dictionary<int, DrainageNode>(Nodes.Count);
            foreach (var n in Nodes) m_ById[n.Id] = n;

            m_Out = new Dictionary<int, List<DrainageEdge>>();
            foreach (var e in Edges)
            {
                List<DrainageEdge> list;
                if (!m_Out.TryGetValue(e.FromNode, out list))
                    m_Out[e.FromNode] = list = new List<DrainageEdge>();
                list.Add(e);
            }
        }

        public DrainageNode NodeById(int id)
        {
            EnsureIndex();
            DrainageNode n;
            return m_ById.TryGetValue(id, out n) ? n : null;
        }

        /// <summary>
        /// Прослеживает путь вниз по течению до законного выпуска.
        /// На развилке идёт по большему падению отметки; при равном падении -
        /// по меньшему Id ребра, чтобы результат не прыгал между прогонами
        /// (от него зависит раскраска бассейнов).
        /// </summary>
        public TraceResult Trace(int startNodeId)
        {
            var result = new TraceResult();
            EnsureIndex();

            if (!m_ById.ContainsKey(startNodeId)) return result;

            var visited = new HashSet<int>();
            int current = startNodeId;

            while (true)
            {
                if (!visited.Add(current))
                {
                    // Замыкание - не исключение, а диагноз: ошибка в отметках.
                    result.Outcome = TraceOutcome.Cycle;
                    return result;
                }

                var node = m_ById[current];
                if (node.Outfall != OutfallKind.None)
                {
                    result.Outcome = TraceOutcome.ReachedOutfall;
                    result.OutfallNode = current;
                    return result;
                }

                List<DrainageEdge> outgoing;
                if (!m_Out.TryGetValue(current, out outgoing) || outgoing.Count == 0)
                {
                    result.Outcome = TraceOutcome.DeadEnd;
                    return result;
                }

                var best = PickBest(node, outgoing);
                if (outgoing.Count > 1) NoteSplit(result, best, outgoing);

                result.Path.Add(best.Id);
                result.TotalLength += best.Length;
                if (best.GradePermille < result.WorstGradePermille)
                    result.WorstGradePermille = best.GradePermille;
                if (best.Confidence > result.WorstConfidence)
                    result.WorstConfidence = best.Confidence;

                current = best.ToNode;
            }
        }

        private DrainageEdge PickBest(DrainageNode from, List<DrainageEdge> outgoing)
        {
            DrainageEdge best = null;
            double bestDrop = double.MinValue;

            foreach (var e in outgoing)
            {
                var to = m_ById.ContainsKey(e.ToNode) ? m_ById[e.ToNode] : null;
                double drop = to == null ? 0.0 : from.Z - to.Z;

                if (best == null || drop > bestDrop + 1e-9 ||
                    (Math.Abs(drop - bestDrop) <= 1e-9 && e.Id < best.Id))
                {
                    best = e;
                    bestDrop = drop;
                }
            }
            return best;
        }

        /// <summary>
        /// Развилка помечается, только если побочная ветка уходит к ДРУГОМУ выпуску.
        /// Две ветки, сходящиеся в тот же выпуск, - не расщепление стока, а просто
        /// два пути к одному месту, тревожить проектировщика незачем.
        /// </summary>
        private void NoteSplit(TraceResult result, DrainageEdge taken, List<DrainageEdge> outgoing)
        {
            int mainOutfall = FollowToOutfall(taken.ToNode);
            foreach (var e in outgoing)
            {
                if (e.Id == taken.Id) continue;
                if (FollowToOutfall(e.ToNode) != mainOutfall) { result.Splits = true; return; }
            }
        }

        /// <summary>Куда придёт вода из этого узла. -1 если никуда. Без учёта Splits.</summary>
        private int FollowToOutfall(int nodeId)
        {
            var seen = new HashSet<int>();
            int current = nodeId;
            while (true)
            {
                if (!seen.Add(current)) return -1;
                DrainageNode node;
                if (!m_ById.TryGetValue(current, out node)) return -1;
                if (node.Outfall != OutfallKind.None) return current;

                List<DrainageEdge> outgoing;
                if (!m_Out.TryGetValue(current, out outgoing) || outgoing.Count == 0) return -1;
                current = PickBest(node, outgoing).ToNode;
            }
        }
    }
}
