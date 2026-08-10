using System;
using System.Collections.Generic;
using AbrRunoff.Core.Network;
using AbrRunoff.Entity;
using AbrRunoff.Report;
using Topomatic.Dwg;
using Topomatic.ToolBridge;

namespace AbrRunoff.Mcp
{
    /// <summary>MCP-инструменты модуля «Схема стока». Публикуются мостом Topomatic.ToolBridge
    /// через broadcast "tool_request". Читают те же данные, что и ведомости; ничего не строят
    /// и в чертёж не пишут.</summary>
    internal sealed partial class RunoffTools : ToolProvider
    {
        [ToolDef(
            Name = "runoff_list_schemes",
            Description = "[СХЕМА СТОКА] Возвращает объекты водоотвода активного чертежа: схемы стока " +
                          "(по одной на дорогу) и сети водоотвода. Поле road отсюда - то, чем адресуется " +
                          "инструмент runoff_problems. isStale = true означает, что дорога не открыта " +
                          "и схема показывает снапшот, а не текущий расчёт.",
            InputSchema = @"{
              'type': 'object',
              'properties': {},
              'additionalProperties': false
            }",
            ReadOnlyHint = true)]
        private object ListSchemes(Dictionary<string, object> args)
        {
            var drawing = RequireDrawing();

            var schemes  = new List<SchemeDto>();
            var networks = new List<NetworkDto>();

            foreach (var e in drawing.ActiveSpace)
            {
                var scheme = e as DwgRunoffScheme;
                if (scheme != null)
                {
                    var rows = RunoffReport.Build(scheme.RoadName, scheme.Result);
                    schemes.Add(RunoffToolsMapping.Scheme(scheme.RoadName, scheme.IsStale, rows));
                    continue;
                }

                var net = e as DwgDrainageNetwork;
                if (net != null)
                    networks.Add(RunoffToolsMapping.Network(SourceNames(net), net.Network, net.Basins));
            }

            return new
            {
                result = new { schemes = schemes.ToArray(), networks = networks.ToArray() },
                description = "Объекты водоотвода активного чертежа. Длины - в метрах плана. " +
                              "problems - число проблемных строк ведомости этого объекта; " +
                              "подробности даёт runoff_problems.",
                status = schemes.Count == 0 && networks.Count == 0
                    ? "В чертеже нет объектов водоотвода. Схему строит команда «Схема стока», сеть - «Сеть водоотвода»."
                    : "Схем: " + schemes.Count + ", сетей: " + networks.Count + "."
            };
        }

        [ToolDef(
            Name = "runoff_problems",
            Description = "[СХЕМА СТОКА] Возвращает ТОЛЬКО проблемные участки водоотвода активного чертежа: " +
                          "нулевой уклон (вода стоит), уклон ниже нормы, бессточное понижение без трубы, " +
                          "труба выше дна кювета, труба вне водоотвода, тупиковый бассейн, догаданная связь. " +
                          "Строки с kind=flow адресуются дорогой и пикетажем, с kind=network - координатами плана. " +
                          "Пустой список означает, что нарушений не найдено.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'road': {
                  'type': 'string',
                  'description': 'Имя дороги из runoff_list_schemes. Без него - весь чертёж.'
                }
              },
              'additionalProperties': false
            }",
            ReadOnlyHint = true)]
        private object Problems(Dictionary<string, object> args)
        {
            var drawing  = RequireDrawing();
            string road  = JsonUtils.GetString(args, "road", null);

            var pipeIssues = new List<ProblemDto>();
            var rest       = new List<ProblemDto>();

            foreach (var e in drawing.ActiveSpace)
            {
                var scheme = e as DwgRunoffScheme;
                if (scheme != null)
                {
                    if (road != null && !string.Equals(scheme.RoadName, road, StringComparison.OrdinalIgnoreCase))
                        continue;

                    foreach (var r in RunoffReport.Build(scheme.RoadName, scheme.Result))
                        if (r.IsProblem) rest.Add(RunoffToolsMapping.Problem(r));
                    continue;
                }

                var net = e as DwgDrainageNetwork;
                if (net == null) continue;

                // Трубы с изъяном - первыми, как в ведомости: они не входят в цепочки,
                // и молча выпавшая из расчёта труба хуже любой другой ошибки.
                foreach (var r in net.PipeProblemRows())
                    pipeIssues.Add(RunoffToolsMapping.Problem(r));

                foreach (var r in NetworkReport.Build(net.Network, net.Basins))
                    if (r.IsProblem) rest.Add(RunoffToolsMapping.Problem(r));
            }

            var problems = new List<ProblemDto>(pipeIssues);
            problems.AddRange(rest);

            return new
            {
                result = new { problems = problems.ToArray() },
                description = "Проблемы водоотвода. Длины - метры плана, уклоны - промилле, " +
                              "координаты - мировые метры плана. Поле confidence: Explicit - связь взята " +
                              "из модели, Physical - труба с собственными отметками, Inferred - " +
                              "геометрическая догадка модуля, её стоит проверить глазами.",
                status = problems.Count == 0
                    ? "Нарушений не найдено."
                    : "Найдено проблем: " + problems.Count + "."
            };
        }

        [ToolDef(
            Name = "runoff_basins",
            Description = "[СХЕМА СТОКА] Возвращает бассейны водосбора каждой сети активного чертежа: " +
                          "номер, есть ли выпуск, вид выпуска, отметка выпуска, суммарная длина сети " +
                          "бассейна и число узлов. Бассейн с hasOutfall = false - вода собирается " +
                          "и никуда не уходит, это дефект проекта.",
            InputSchema = @"{
              'type': 'object',
              'properties': {},
              'additionalProperties': false
            }",
            ReadOnlyHint = true)]
        private object Basins(Dictionary<string, object> args)
        {
            var drawing = RequireDrawing();
            var groups  = new List<object>();
            int total   = 0;

            foreach (var e in drawing.ActiveSpace)
            {
                var net = e as DwgDrainageNetwork;
                if (net == null) continue;

                var infos  = BasinSummary.Build(net.Network, net.Basins);
                var basins = new List<BasinDto>();
                foreach (var info in infos) basins.Add(RunoffToolsMapping.Basin(info));
                total += basins.Count;

                groups.Add(new { sources = SourceNames(net).ToArray(), basins = basins.ToArray() });
            }

            return new
            {
                result = new { networks = groups.ToArray() },
                description = "Бассейны по сетям чертежа. Длины - метры плана, отметки - метры. " +
                              "outfallKind: BeyondAlignment - сброс за пределы трассы, PipeOutward - труба " +
                              "в сторону от дороги, StormWell - колодец или ливневая канализация, " +
                              "None - выпуска нет.",
                status = total == 0
                    ? "В чертеже нет сетей водоотвода. Сеть строит команда «Сеть водоотвода»."
                    : "Бассейнов: " + total + "."
            };
        }

        [ToolDef(
            Name = "runoff_trace_flow",
            Description = "[СХЕМА СТОКА] Прослеживает сток от указанной точки плана вниз по течению. " +
                          "Возвращает исход (reachedOutfall - вода доходит до выпуска, deadEnd - " +
                          "не доходит, cycle - кольцевой сток, ошибка отметок), длину пути, худший уклон " +
                          "и координату выпуска. Поле startNode показывает, к какому узлу сети точка была " +
                          "привязана фактически - сверяйте с тем, что имели в виду.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'x': { 'type': 'number', 'description': 'Мировая координата X плана, метры.' },
                'y': { 'type': 'number', 'description': 'Мировая координата Y плана, метры.' }
              },
              'required': ['x', 'y'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true)]
        private object TraceFlow(Dictionary<string, object> args)
        {
            var drawing = RequireDrawing();
            double x = JsonUtils.RequireDouble(args, "x");
            double y = JsonUtils.RequireDouble(args, "y");

            DwgDrainageNetwork bestNet = null;
            DrainageNode bestNode = null;
            double bestDist = double.MaxValue;

            foreach (var e in drawing.ActiveSpace)
            {
                var net = e as DwgDrainageNetwork;
                if (net == null || net.Network == null) continue;

                foreach (var n in net.Network.Nodes)
                {
                    double dx = n.X - x, dy = n.Y - y;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    if (d >= bestDist) continue;
                    bestDist = d; bestNode = n; bestNet = net;
                }
            }

            // Допуск шире сшивки в пять раз: точка от агента - не клик по объекту,
            // но ответ по узлу за сотни метров был бы выдумкой с видом факта.
            double limit = bestNet != null ? bestNet.LinkTolerance * 5.0 : 0.0;
            if (bestNode == null || bestDist > limit)
            {
                return new
                {
                    result = new { },
                    description = "Трассировка не выполнялась.",
                    status = bestNode == null
                        ? "В чертеже нет сетей водоотвода. Сеть строит команда «Сеть водоотвода»."
                        : "Рядом с точкой нет элементов сети: ближайший узел в " +
                          bestDist.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                          " м. Уточните координату по runoff_problems или runoff_basins."
                };
            }

            var trace = bestNet.Network.Trace(bestNode.Id);

            double? ox = null, oy = null, oz = null;
            if (trace.OutfallNode >= 0)
            {
                foreach (var n in bestNet.Network.Nodes)
                    if (n.Id == trace.OutfallNode) { ox = n.X; oy = n.Y; oz = n.Z; break; }
            }

            var dto = RunoffToolsMapping.Trace(trace, bestNode.X, bestNode.Y, bestDist, ox, oy, oz);

            return new
            {
                result = dto,
                description = "Путь стока вниз по течению. Длина - метры, уклон - промилле, " +
                              "координаты - мировые метры плана. worstConfidence - достоверность " +
                              "слабейшего звена цепочки: Inferred означает геометрическую догадку модуля. " +
                              "splits = true - по пути есть развилка к ДРУГОМУ выпуску.",
                status = dto.Outcome == "reachedOutfall"
                    ? "Сток доходит до выпуска."
                    : dto.Outcome == "cycle"
                        ? "Кольцевой сток - проверьте отметки."
                        : "Сток не доходит до выпуска."
            };
        }

        /// <summary>Имена моделей-источников сети - её человекочитаемый адрес.</summary>
        private static List<string> SourceNames(DwgDrainageNetwork net)
        {
            var names = new List<string>();
            names.AddRange(net.RoadNames);
            names.AddRange(net.SiteNames);
            names.AddRange(net.NetworkNames);
            return names;
        }

        /// <summary>Активный чертёж. Текст ошибки - как у штатных инструментов моста.</summary>
        private Drawing RequireDrawing()
        {
            var drawing = DwgUtils.GetDrawing(CadView);
            if (drawing == null) throw new InvalidOperationException("Не удалось получить активный чертеж.");
            return drawing;
        }
    }
}
