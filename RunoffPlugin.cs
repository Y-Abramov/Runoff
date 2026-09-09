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

        [cmd("runoff_watershed")]
        public void Watershed()
        {
            var surfaces = Robur.SurfaceAccess.GetSurfaces();
            if (surfaces.Count == 0)
            {
                MessageDlg.Show("В проекте нет моделей рельефа. Загрузите съёмку или постройте ЦММ по открытым данным (модуль DemLoader).");
                return;
            }

            var drawing = ActiveDrawing();
            if (drawing == null) { MessageDlg.Show("Не найден активный чертёж."); return; }

            var culverts = Robur.CulvertAccess.GetOpenCulverts();
            UI.WatershedDialogState state = null;

            // Указание точки на плане закрывает диалог (DialogResult.Retry) вместо
            // пикания изнутри открытого модального окна: последнее ненадёжно у
            // разных хозяев окна (см. историю правок). Пикаем точку тем же приёмом,
            // что и остальные команды модуля, и открываем диалог заново с состоянием.
            while (true)
            {
                using (var dlg = new UI.WatershedDialog(surfaces, culverts, new Core.Watershed.WatershedSettings(), state))
                {
                    var result = dlg.ShowDialog();

                    if (result == System.Windows.Forms.DialogResult.Retry)
                    {
                        state = dlg.CaptureState();
                        Topomatic.Cad.Foundation.Vector3D point;
                        if (Topomatic.Cad.View.Hints.CadCursors.GetPoint(CadView, out point, "Точка замыкающего створа:"))
                        {
                            state.ManualOutlets.Add(new Robur.WatershedRequest
                            {
                                OutletName = "Точка " + (state.ManualOutlets.Count + 1),
                                X = point.X,
                                Y = point.Y
                            });
                        }
                        continue;
                    }

                    if (result != System.Windows.Forms.DialogResult.OK) return;

                    var errors = new System.Collections.Generic.List<string>();
                    foreach (var built in dlg.Results)
                    {
                        if (built.Result == null) { errors.Add(built.OutletName + ": " + built.Error); continue; }

                        var entity = new Entity.DwgWatershed();
                        entity.SetResult(built.SurfaceName, built.DesignSurfaceName, built.OutletName,
                                         built.OutletX, built.OutletY, built.Step, built.Settings,
                                         built.SourceHash, built.Result);
                        drawing.ActiveSpace.Add(entity);
                    }

                    CadView.Unlock();
                    CadView.Invalidate();

                    if (errors.Count > 0)
                        MessageDlg.Show("Не удалось построить:\r\n" + string.Join("\r\n", errors.ToArray()));
                    return;
                }
            }
        }

        [cmd("runoff_watershed_update")]
        public void WatershedUpdate()
        {
            var drawing = ActiveDrawing();
            if (drawing == null) { MessageDlg.Show("Не найден активный чертёж."); return; }

            var surfaces = Robur.SurfaceAccess.GetSurfaces();
            int updated = 0;

            foreach (Topomatic.Dwg.Entities.DwgEntity e in drawing.ActiveSpace)
            {
                var ws = e as Entity.DwgWatershed;
                if (ws == null || ws.Snapshot == null) continue;

                var surface = FindSurface(surfaces, ws.SurfaceName);
                if (surface == null) continue;   // поверхность закрыта/удалена - не трогаем
                var design = string.IsNullOrEmpty(ws.DesignSurfaceName) ? null : FindSurface(surfaces, ws.DesignSurfaceName);

                string currentHash = Robur.SurfaceReader.ComputeSourceHash(surface, design, ws.OutletX, ws.OutletY, ws.Step);
                ws.CheckStale(currentHash);
                if (!ws.IsStale) continue;

                var request = new Robur.WatershedRequest { OutletName = ws.OutletName, X = ws.OutletX, Y = ws.OutletY };
                var built = Robur.WatershedBuilder.Build(surface, design, ws.Step, ws.Settings,
                    new System.Collections.Generic.List<Robur.WatershedRequest> { request }, null);

                if (built.Count == 1 && built[0].Result != null)
                {
                    ws.SetResult(built[0].SurfaceName, built[0].DesignSurfaceName, built[0].OutletName,
                                built[0].OutletX, built[0].OutletY, built[0].Step, built[0].Settings,
                                built[0].SourceHash, built[0].Result);
                    updated++;
                }
            }

            CadView.Unlock();
            CadView.Invalidate();
            MessageDlg.Show(updated == 0 ? "Устаревших водосборов не найдено." : "Пересчитано водосборов: " + updated + ".");
        }

        [cmd("runoff_watershed_report")]
        public void WatershedReportCmd()
        {
            if (s_WatershedReportForm != null && !s_WatershedReportForm.IsDisposed)
            {
                s_WatershedReportForm.BringToFront();
                return;
            }

            var drawing = ActiveDrawing();
            if (drawing == null) { MessageDlg.Show("Не найден активный чертёж."); return; }

            var entities = new System.Collections.Generic.List<Entity.DwgWatershed>();
            foreach (Topomatic.Dwg.Entities.DwgEntity e in drawing.ActiveSpace)
            {
                var ws = e as Entity.DwgWatershed;
                if (ws != null && ws.Snapshot != null) entities.Add(ws);
            }

            if (entities.Count == 0)
            {
                MessageDlg.Show("В чертеже нет водосборов. Постройте их командой «Водосбор».");
                return;
            }

            var view = CadView;
            s_WatershedReportForm = new UI.WatershedReportForm(entities);
            s_WatershedReportForm.PlaceTable = delegate (System.Collections.Generic.List<ReportNs.WatershedRow> r)
            {
                Topomatic.Cad.Foundation.Vector3D point;
                if (!Topomatic.Cad.View.Hints.CadCursors.GetPoint(view, out point, "Точка вставки таблицы:"))
                    return;
                ReportNs.ReportTablePlacer.Place(drawing.ActiveSpace,
                    new Topomatic.Cad.Foundation.Vector2D(point.X, point.Y), r, 1.5);
                view.Unlock();
                view.Invalidate();
            };
            s_WatershedReportForm.Show();
        }

        [cmd("runoff_watershed_explode")]
        public void WatershedExplode()
        {
            var ws = FirstSelectedWatershed();
            if (ws == null) { MessageDlg.Show("Выберите водосбор на плане и повторите команду."); return; }
            if (ws.Snapshot == null) { MessageDlg.Show("Водосбор ещё не рассчитан."); return; }

            var drawing = ActiveDrawing();
            if (drawing == null) { MessageDlg.Show("Не найден активный чертёж."); return; }

            // Детальная геометрия: взрыв делают для передачи, LOD там ни к чему.
            var block = ws.GetPlanBlock(true);
            if (block == null || block.Count == 0)
            {
                MessageDlg.Show("Водосбор пуст, взрывать нечего.");
                return;
            }

            var space = drawing.ActiveSpace;
            foreach (Topomatic.Dwg.Entities.DwgEntity src in block)
            {
                var copy = src.Clone() as Topomatic.Dwg.Entities.DwgEntity;
                if (copy != null) space.Add(copy);
            }
            space.Entities.Remove(ws);

            CadView.Unlock();
            CadView.Invalidate();
        }

        private static UI.WatershedReportForm s_WatershedReportForm;

        private Entity.DwgWatershed FirstSelectedWatershed()
        {
            var drawing = ActiveDrawing();
            if (drawing == null) return null;
            Entity.DwgWatershed any = null;
            try
            {
                foreach (Topomatic.Dwg.Entities.DwgEntity e in drawing.ActiveSpace)
                {
                    var w = e as Entity.DwgWatershed;
                    if (w == null) continue;
                    if (w.IsSelected) return w;
                    if (any == null) any = w;
                }
            }
            catch { }
            return any;
        }

        private static Robur.SurfaceRef FindSurface(System.Collections.Generic.List<Robur.SurfaceRef> surfaces, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var s in surfaces)
                if (string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) return s;
            return null;
        }
    }
}
