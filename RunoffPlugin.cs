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
            Entity.RunoffLegend.Build(
                new Topomatic.Cad.Foundation.Vector2D(point.X, point.Y), settings,
                delegate (Topomatic.Dwg.Entities.DwgEntity e) { space.Add(e); });

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

        [cmd("runoff_pick_lines")]
        public void PickLines()
        {
            MessageDlg.Show("Выбор структурных линий Площадки доступен после подтверждения " +
                            "доступа к модели (см. runoff/CLAUDE.md).");
        }

        [cmd("runoff_network_report")]
        public void NetworkReportCmd()
        {
            var net = FirstSelectedNetwork();
            if (net == null) { MessageDlg.Show("Выберите сеть водоотвода на плане и повторите команду."); return; }

            var rows = ReportNs.NetworkReport.Build(net.Network, net.Basins);
            if (rows.Count == 0) { MessageDlg.Show("Сеть пуста."); return; }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(string.Join(";", ReportNs.NetworkReport.Header()));
            foreach (var r in rows) sb.AppendLine(string.Join(";", ReportNs.NetworkReport.ToCells(r)));

            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "abr_runoff_network.csv");
            // UTF-8 с BOM: без него RU-Excel ломает кириллицу.
            System.IO.File.WriteAllText(path, sb.ToString(), new System.Text.UTF8Encoding(true));
            try { System.Diagnostics.Process.Start(path); } catch { MessageDlg.Show("Ведомость: " + path); }
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

        // ВРЕМЕННАЯ. Удалить после закрытия гейта Task 0.
        [cmd("runoff_probe2")]
        public void Probe2()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "abr_runoff_probe2.txt");
            var sb = new System.Text.StringBuilder();
            try
            {
                sb.AppendLine("=== ABR | Runoff v2: разведка моделей ===");
                sb.AppendLine("Дата: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine();

                Robur.ModelProbe.DumpModels(sb);
                sb.AppendLine();

                var sw = System.Diagnostics.Stopwatch.StartNew();
                int nodeCount = 0;
                foreach (var rr in Robur.RoadAccess.GetOpenRoads())
                {
                    Robur.ModelProbe.DumpPipes(sb, rr.Road, rr.Name);
                    sb.AppendLine();

                    var settings = new Core.RunoffSettings();
                    var stations = Robur.DitchReader.BuildStations(rr.Road, settings.SampleStep);
                    nodeCount += stations.Count * 2;
                }
                sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "ВРЕМЯ: узлов-кандидатов {0}, построение {1} мс", nodeCount, sw.ElapsedMilliseconds));
            }
            catch (Exception ex) { sb.AppendLine("FATAL: " + ex); }

            System.IO.File.WriteAllText(path, sb.ToString(), new System.Text.UTF8Encoding(true));
            try { System.Diagnostics.Process.Start("notepad.exe", "\"" + path + "\""); } catch { }
        }

    }
}
