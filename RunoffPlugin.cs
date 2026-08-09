using System;
using Topomatic.ApplicationPlatform.Plugins;
using Topomatic.Controls.Dialogs;
using ReportNs = AbrRunoff.Report;

namespace AbrRunoff
{
    public partial class RunoffPlugin : PluginInitializator
    {
        public override void Initialize(PluginFactory factory)
        {
            base.Initialize(factory);
            try { Abr.Bootstrap.AbrBootstrap.Attach("Схема стока", null, null); }
            catch { }
        }

        [cmd("runoff_add")]
        public void Add()
        {
            var roads = Robur.RoadAccess.GetOpenRoads();
            if (roads.Count == 0)
            {
                MessageDlg.Show("В проекте нет открытых дорожных моделей.");
                return;
            }

            var drawing = ActiveDrawing();
            if (drawing == null)
            {
                MessageDlg.Show("Не найден активный чертёж.");
                return;
            }

            using (var dlg = new UI.RoadSelectDialog(roads, RunoffSettingsStore.Load()))
            {
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                RunoffSettingsStore.Save(dlg.Settings);

                foreach (var rr in dlg.Selected)
                {
                    var scheme = new Entity.DwgRunoffScheme();
                    scheme.RoadName = rr.Name;
                    scheme.SetSettings(dlg.Settings.Clone());
                    drawing.ActiveSpace.Add(scheme);
                }
            }
        }

        [cmd("runoff_edit")]
        public void Edit()
        {
            var scheme = FirstSelectedScheme();
            if (scheme == null)
            {
                MessageDlg.Show("Выберите схему стока на плане и повторите команду.");
                return;
            }
            using (var dlg = new UI.SchemeSettingsDialog(scheme.Settings))
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    scheme.SetSettings(dlg.Value);
        }

        private Topomatic.Dwg.Drawing ActiveDrawing()
        {
            try
            {
                var layer = Topomatic.Dwg.Layer.DrawingLayer.GetDrawingLayer(CadView)
                            as Topomatic.Dwg.Layer.DrawingLayer;
                return layer == null ? null : layer.Drawing;
            }
            catch { return null; }
        }

        private Entity.DwgRunoffScheme FirstSelectedScheme()
        {
            var drawing = ActiveDrawing();
            if (drawing == null) return null;
            Entity.DwgRunoffScheme any = null;
            try
            {
                foreach (Topomatic.Dwg.Entities.DwgEntity e in drawing.ActiveSpace)
                {
                    var s = e as Entity.DwgRunoffScheme;
                    if (s == null) continue;
                    if (s.IsSelected) return s;
                    if (any == null) any = s;
                }
            }
            catch { }
            // Ничего не выбрано, но схема в чертеже одна - работаем с ней.
            return any;
        }

        private static UI.ReportForm s_ReportForm;
        private static UI.NetworkReportForm s_NetworkReportForm;

        [cmd("runoff_report")]
        public void Report()
        {
            if (s_ReportForm != null && !s_ReportForm.IsDisposed)
            {
                s_ReportForm.BringToFront();
                return;
            }

            var drawing = ActiveDrawing();
            if (drawing == null) { MessageDlg.Show("Не найден активный чертёж."); return; }

            var rows = new System.Collections.Generic.List<ReportNs.ReportRow>();
            foreach (var e in drawing.ActiveSpace)
            {
                var scheme = e as Entity.DwgRunoffScheme;
                if (scheme == null) continue;
                rows.AddRange(ReportNs.RunoffReport.Build(scheme.RoadName, scheme.Result));
            }

            if (rows.Count == 0)
            {
                MessageDlg.Show("В чертеже нет схем стока. Постройте схему командой «Схема стока».");
                return;
            }

            var view = CadView;
            s_ReportForm = new UI.ReportForm(rows);
            s_ReportForm.PlaceTable = delegate (System.Collections.Generic.List<ReportNs.ReportRow> r)
            {
                Topomatic.Cad.Foundation.Vector3D point;
                if (!Topomatic.Cad.View.Hints.CadCursors.GetPoint(view, out point, "Точка вставки таблицы:"))
                    return;
                ReportNs.ReportTablePlacer.Place(drawing.ActiveSpace,
                    new Topomatic.Cad.Foundation.Vector2D(point.X, point.Y), r, 1.5);
                view.Unlock();
                view.Invalidate();
            };
            s_ReportForm.ZoomTo = delegate (string road, double from, double to)
            {
                ZoomToStations(view, drawing, road, from, to);
            };
            s_ReportForm.Show();
        }

        /// <summary>
        /// Зум к участку: считаем габарит по точкам дна между пикетами и отдаём его
        /// CadView.ZoomBound (подтверждено рефлексией SDK 16.0.62.12).
        /// BoundingBox2D не имеет пустого конструктора и Min/Max - собираем точки
        /// списком и строим через CreateFromPoints, поля - через Inflate.
        /// </summary>
        private static void ZoomToStations(Topomatic.Cad.View.CadView view, Topomatic.Dwg.Drawing drawing,
                                           string road, double from, double to)
        {
            try
            {
                Robur.RoadAccess.RoadRef target = null;
                foreach (var rr in Robur.RoadAccess.GetOpenRoads())
                    if (string.Equals(rr.Name, road, StringComparison.OrdinalIgnoreCase)) { target = rr; break; }
                if (target == null) return;

                var proj = new Robur.PlanProjector(target.Road);
                var points = new System.Collections.Generic.List<Topomatic.Cad.Foundation.Vector2D>();
                double step = Math.Max(1.0, (to - from) / 20.0);
                for (double st = from; st <= to + 1e-9; st += step)
                {
                    Topomatic.Cad.Foundation.Vector2D p;
                    if (!proj.TryAxis(st, out p)) continue;
                    points.Add(p);
                }
                if (points.Count == 0) return;

                var box = Topomatic.Cad.Foundation.BoundingBox2D.CreateFromPoints(points.ToArray());
                box.Inflate(20.0, 20.0);   // поля вокруг участка, иначе он упрётся в рамку экрана
                view.ZoomBound(box, true);
            }
            catch { }
        }

        [cmd("runoff_explode")]
        public void Explode()
        {
            var scheme = FirstSelectedScheme();
            if (scheme == null)
            {
                MessageDlg.Show("Выберите схему стока на плане и повторите команду.");
                return;
            }

            var drawing = ActiveDrawing();
            if (drawing == null) { MessageDlg.Show("Не найден активный чертёж."); return; }

            // Детальная геометрия: взрыв делают для передачи, LOD там ни к чему.
            var block = scheme.GetPlanBlock(true);
            if (block == null || block.Count == 0)
            {
                MessageDlg.Show("Схема пуста, взрывать нечего.");
                return;
            }

            var space = drawing.ActiveSpace;
            foreach (Topomatic.Dwg.Entities.DwgEntity src in block)
            {
                var copy = src.Clone() as Topomatic.Dwg.Entities.DwgEntity;
                if (copy != null) space.Add(copy);
            }
            space.Entities.Remove(scheme);

            CadView.Unlock();
            CadView.Invalidate();
        }

        [cmd("runoff_legend")]
        public void Legend()
        {
            var drawing = ActiveDrawing();
            if (drawing == null) { MessageDlg.Show("Не найден активный чертёж."); return; }

            // Масштаб знаков берём у существующей схемы: легенда, нарисованная
            // другим размером, чем схема на плане, вводит в заблуждение.
            var scheme = FirstSelectedScheme();
            var settings = scheme != null ? scheme.Settings : RunoffSettingsStore.Load();

            Topomatic.Cad.Foundation.Vector3D point;
            if (!Topomatic.Cad.View.Hints.CadCursors.GetPoint(CadView, out point, "Точка вставки легенды:"))
                return;

            var space = drawing.ActiveSpace;
            var origin = new Topomatic.Cad.Foundation.Vector2D(point.X, point.Y);
            Entity.RunoffLegend.Build(origin, settings,
                delegate (Topomatic.Dwg.Entities.DwgEntity e) { space.Add(e); });

            // Если в чертеже есть сеть - рядом с легендой знаков кладём сводку по
            // её бассейнам. Легенда объясняет знаки, таблица отвечает на вопрос
            // «сколько бассейнов и куда каждый уходит».
            var net = FirstSelectedNetwork();
            if (net != null)
            {
                var basins = AbrRunoff.Core.Network.BasinSummary.Build(net.Network, net.Basins);
                if (basins.Count > 0)
                {
                    double gap = net.Settings.TextHeight * 4.0;
                    var tableAt = new Topomatic.Cad.Foundation.Vector2D(origin.X, origin.Y - gap * 6.0);
                    Entity.BasinTable.Build(tableAt, basins, net.Settings,
                        delegate (Topomatic.Dwg.Entities.DwgEntity e) { space.Add(e); });
                }
            }

            CadView.Unlock();
            CadView.Invalidate();
        }

        [cmd("about_runoff")]
        public void About()
        {
            using (var dlg = new AboutDialog())
                dlg.ShowDialog();
        }

        [cmd("runoff_network")]
        public void BuildNetwork()
        {
            var roads = Robur.RoadAccess.GetOpenRoads();
            if (roads.Count == 0) { MessageDlg.Show("В проекте нет открытых дорожных моделей."); return; }

            var drawing = ActiveDrawing();
            if (drawing == null) { MessageDlg.Show("Не найден активный чертёж."); return; }

            var names = new System.Collections.Generic.List<string>();
            foreach (var rr in roads) names.Add(rr.Name);

            using (var dlg = new UI.NetworkSourceDialog(names, RunoffSettingsStore.Load()))
            {
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                var net = new Entity.DwgDrainageNetwork();
                net.RoadNames.AddRange(dlg.SelectedRoads);
                net.SetSettings(dlg.Settings.Clone());
                net.LinkTolerance = dlg.LinkTolerance;
                drawing.ActiveSpace.Add(net);

                CadView.Unlock();
                CadView.Invalidate();
            }
        }

        [cmd("runoff_network_edit")]
        public void EditNetwork()
        {
            var net = FirstSelectedNetwork();
            if (net == null) { MessageDlg.Show("Выберите сеть водоотвода на плане и повторите команду."); return; }

            using (var dlg = new UI.SchemeSettingsDialog(net.Settings))
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    net.SetSettings(dlg.Value);
        }

        [cmd("runoff_trace")]
        public void TraceFlow()
        {
            var net = FirstSelectedNetwork();
            if (net == null) { MessageDlg.Show("Выберите сеть водоотвода на плане и повторите команду."); return; }

            Topomatic.Cad.Foundation.Vector3D point;
            if (!Topomatic.Cad.View.Hints.CadCursors.GetPoint(CadView, out point, "Точка на элементе сети:"))
                return;

            var graph = net.Network;
            int nearest = -1;
            double bestDist = double.MaxValue;
            foreach (var n in graph.Nodes)
            {
                double dx = n.X - point.X, dy = n.Y - point.Y;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d >= bestDist) continue;
                bestDist = d; nearest = n.Id;
            }
            if (nearest < 0) { MessageDlg.Show("Рядом нет элементов сети."); return; }

            var trace = graph.Trace(nearest);
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            string outcome = trace.Outcome == Core.Network.TraceOutcome.ReachedOutfall
                ? "Сток доходит до выпуска."
                : trace.Outcome == Core.Network.TraceOutcome.Cycle
                    ? "Кольцевой сток - проверьте отметки."
                    : "Сток не доходит до выпуска.";

            MessageDlg.Show(string.Format(ci,
                "{0}\nДлина пути: {1:F1} м\nХудший уклон: {2:F2} промилле\nЗвеньев: {3}",
                outcome, trace.TotalLength,
                trace.WorstGradePermille == double.MaxValue ? 0.0 : trace.WorstGradePermille,
                trace.Path.Count));
        }

        // Команда `runoff_pick_lines` (отбор структурных линий Площадки) УДАЛЕНА
        // 2026-08-09 вместе с кнопкой: она была заглушкой и показывала сообщение
        // вместо работы. Пустая кнопка в релизе хуже отсутствующей - юзер жмёт её
        // и получает отписку. Вернуть вместе с реализацией отбора линий; путь до
        // данных подтверждён (SiteModel -> Surface -> StructureLines), не хватает
        // только правила, какие из 272 линий `Канавы.site` считать водоотводом.

        [cmd("runoff_network_report")]
        public void NetworkReportCmd()
        {
            if (s_NetworkReportForm != null && !s_NetworkReportForm.IsDisposed)
            {
                s_NetworkReportForm.BringToFront();
                return;
            }

            var net = FirstSelectedNetwork();
            if (net == null) { MessageDlg.Show("Выберите сеть водоотвода на плане и повторите команду."); return; }

            var drawing = ActiveDrawing();
            if (drawing == null) { MessageDlg.Show("Не найден активный чертёж."); return; }

            var rows = ReportNs.NetworkReport.Build(net.Network, net.Basins);

            // Трубы с изъяном - отдельными строками, первыми: они не входят в
            // цепочки, но проектировщик обязан о них узнать.
            var pipeProblems = net.PipeProblemRows();
            if (pipeProblems.Count > 0) rows.InsertRange(0, pipeProblems);

            if (rows.Count == 0) { MessageDlg.Show("Сеть пуста."); return; }

            var view = CadView;
            s_NetworkReportForm = new UI.NetworkReportForm(rows);
            s_NetworkReportForm.PlaceTable = delegate (System.Collections.Generic.List<ReportNs.NetworkRow> r)
            {
                Topomatic.Cad.Foundation.Vector3D point;
                if (!Topomatic.Cad.View.Hints.CadCursors.GetPoint(view, out point, "Точка вставки таблицы:"))
                    return;
                ReportNs.ReportTablePlacer.Place(drawing.ActiveSpace,
                    new Topomatic.Cad.Foundation.Vector2D(point.X, point.Y), r, 1.5);
                view.Unlock();
                view.Invalidate();
            };
            s_NetworkReportForm.ZoomTo = delegate (ReportNs.NetworkRow r)
            {
                ZoomToChain(view, r);
            };
            s_NetworkReportForm.Show();
        }

        /// <summary>
        /// Зум к цепочке сети. Пикетажа у сети нет - габарит собираем прямо по
        /// координатам начала и выпуска. Тупик выпуска не имеет, тогда зумим к
        /// одному началу: показать нечего, но найти проблемное место надо.
        /// </summary>
        private static void ZoomToChain(Topomatic.Cad.View.CadView view, ReportNs.NetworkRow row)
        {
            try
            {
                if (row == null) return;

                var points = new System.Collections.Generic.List<Topomatic.Cad.Foundation.Vector2D>
                {
                    new Topomatic.Cad.Foundation.Vector2D(row.X, row.Y)
                };
                if (row.HasOutfallPos)
                    points.Add(new Topomatic.Cad.Foundation.Vector2D(row.OutfallX, row.OutfallY));

                var box = Topomatic.Cad.Foundation.BoundingBox2D.CreateFromPoints(points.ToArray());
                box.Inflate(20.0, 20.0);   // поля, иначе цепочка упрётся в рамку экрана
                view.ZoomBound(box, true);
            }
            catch { }
        }

        private Entity.DwgDrainageNetwork FirstSelectedNetwork()
        {
            var drawing = ActiveDrawing();
            if (drawing == null) return null;
            Entity.DwgDrainageNetwork any = null;
            try
            {
                foreach (Topomatic.Dwg.Entities.DwgEntity e in drawing.ActiveSpace)
                {
                    var n = e as Entity.DwgDrainageNetwork;
                    if (n == null) continue;
                    if (n.IsSelected) return n;
                    if (any == null) any = n;
                }
            }
            catch { }
            return any;
        }

    }
}
