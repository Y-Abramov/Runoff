using System.Collections.Generic;
using AbrRunoff.Core.Network;
using AbrRunoff.Report;

namespace AbrRunoff.Mcp
{
    /// <summary>
    /// Выдача MCP-инструментов модуля. Без ссылок на сборки Robur СОЗНАТЕЛЬНО:
    /// файл линкуется в Runoff.Tests и проверяется без установленного САПР, как и Core с Report.
    /// Здесь нет предметной логики - только форма ответа агенту.
    /// </summary>
    public static class RunoffToolsMapping
    {
        public static SchemeDto Scheme(string road, bool isStale, IList<ReportRow> rows)
        {
            int problems = 0;
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].IsProblem) problems++;

            return new SchemeDto
            {
                Road     = road,
                IsStale  = isStale,
                Segments = rows.Count,
                Problems = problems
            };
        }

        public static NetworkDto Network(IList<string> sources, DrainageNetwork net,
                                         IDictionary<int, int> basins)
        {
            var dto = new NetworkDto { Sources = new List<string>(sources).ToArray() };
            if (net == null) return dto;

            dto.Nodes = net.Nodes.Count;
            dto.Edges = net.Edges.Count;

            if (basins == null) return dto;

            var infos = BasinSummary.Build(net, new Dictionary<int, int>(basins));
            dto.Basins = infos.Count;
            for (int i = 0; i < infos.Count; i++)
                if (infos[i].IsProblem) dto.ProblemBasins++;

            return dto;
        }

        public static ProblemDto Problem(ReportRow r)
        {
            return new ProblemDto
            {
                Kind          = "flow",
                Road          = r.Road,
                Side          = r.Side,
                StationFrom   = r.StationFrom,
                StationTo     = r.StationTo,
                Length        = r.Length,
                GradePermille = r.GradePermille,
                Status        = r.Status
            };
        }

        public static ProblemDto Problem(NetworkRow r)
        {
            return new ProblemDto
            {
                Kind          = "network",
                Source        = r.Source,
                Basin         = r.Basin,
                X             = r.X,
                Y             = r.Y,
                Length        = r.TotalLength,
                GradePermille = r.WorstGradePermille,
                Confidence    = r.Confidence,
                Status        = r.Status
            };
        }

        public static TraceDto Trace(TraceResult t, double startX, double startY, double distance,
                                     double? outfallX, double? outfallY, double? outfallZ)
        {
            var dto = new TraceDto
            {
                Outcome         = OutcomeName(t.Outcome),
                TotalLength     = t.TotalLength,
                Edges           = t.Path.Count,
                WorstConfidence = t.WorstConfidence.ToString(),
                Splits          = t.Splits,
                StartNode       = new PointDto { X = startX, Y = startY, Distance = distance }
            };

            // MaxValue - служебное «уклона не было»; наружу оно поехало бы числом
            // 1.8e308 и агент принял бы его за измерение.
            if (t.WorstGradePermille != double.MaxValue)
                dto.WorstGradePermille = t.WorstGradePermille;

            if (outfallX.HasValue && outfallY.HasValue)
                dto.Outfall = new PointDto { X = outfallX.Value, Y = outfallY.Value, Z = outfallZ };

            return dto;
        }

        private static string OutcomeName(TraceOutcome outcome)
        {
            switch (outcome)
            {
                case TraceOutcome.ReachedOutfall: return "reachedOutfall";
                case TraceOutcome.Cycle:          return "cycle";
                default:                          return "deadEnd";
            }
        }

        public static BasinDto Basin(BasinInfo b)
        {
            return new BasinDto
            {
                Number      = b.Number,
                HasOutfall  = b.HasOutfall,
                OutfallKind = b.OutfallKind.ToString(),
                // Отметка выпуска осмысленна только при наличии самого выпуска -
                // у бассейна без выпуска OutfallZ ничего не значит, отдавать 0.0
                // вместо null означало бы врать агенту про реальную отметку.
                OutfallZ    = b.HasOutfall ? (double?)b.OutfallZ : null,
                TotalLength = b.TotalLength,
                NodeCount   = b.NodeCount,
                IsProblem   = b.IsProblem
            };
        }
    }

    /// <summary>Свойства, а не поля: мост сериализует выдачу через публичные свойства.</summary>
    public sealed class SchemeDto
    {
        public string Road { get; set; }
        public bool IsStale { get; set; }
        public int Segments { get; set; }
        public int Problems { get; set; }
    }

    public sealed class NetworkDto
    {
        public string[] Sources { get; set; }
        public int Nodes { get; set; }
        public int Edges { get; set; }
        public int Basins { get; set; }
        public int ProblemBasins { get; set; }
    }

    /// <summary>
    /// Одна проблема водоотвода. Kind различает происхождение: у строки стока есть
    /// пикетаж и нет координат, у строки сети - наоборот (общего пикетажа у сети нет,
    /// её элементы приходят из разных моделей). Незаполненные поля остаются null и
    /// в выдачу не попадают - агенту не нужны нули там, где величины не существует.
    /// </summary>
    public sealed class ProblemDto
    {
        public string Kind { get; set; }
        public string Status { get; set; }

        public string Road { get; set; }
        public string Side { get; set; }
        public double? StationFrom { get; set; }
        public double? StationTo { get; set; }

        public string Source { get; set; }
        public int? Basin { get; set; }
        public double? X { get; set; }
        public double? Y { get; set; }

        public double Length { get; set; }
        public double GradePermille { get; set; }
        public string Confidence { get; set; }
    }

    /// <summary>
    /// Бассейн для агента. Поле Segments исходного BasinInfo сюда НЕ переносится:
    /// это звенья ленты подсветки на плане, на реальной сети их тысячи, а вопрос
    /// «куда сбрасывает бассейн» они не приближают.
    /// </summary>
    public sealed class BasinDto
    {
        public int Number { get; set; }
        public bool HasOutfall { get; set; }
        public string OutfallKind { get; set; }
        public double? OutfallZ { get; set; }
        public double TotalLength { get; set; }
        public int NodeCount { get; set; }
        public bool IsProblem { get; set; }
    }

    public sealed class PointDto
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double? Z { get; set; }

        /// <summary>Расстояние от точки, заданной агентом, до реально найденного узла.</summary>
        public double? Distance { get; set; }
    }

    /// <summary>
    /// Результат трассировки. StartNode обязателен: агент задаёт координату «на глаз»,
    /// ближайшим может оказаться не тот узел, и без обратной связи о том, куда реально
    /// попали, любой ответ выглядит достоверным.
    /// </summary>
    public sealed class TraceDto
    {
        public string Outcome { get; set; }
        public double TotalLength { get; set; }
        public int Edges { get; set; }
        public double? WorstGradePermille { get; set; }
        public string WorstConfidence { get; set; }
        public bool Splits { get; set; }
        public PointDto StartNode { get; set; }
        public PointDto Outfall { get; set; }
    }
}
