using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Abr.Sdk;
using AbrRunoff.Core.Watershed;
using AbrRunoff.Entity;
using AbrRunoff.Report;
using AbrRunoff.Robur;

namespace AbrRunoff.UI
{
    /// <summary>Ведомость водосборов - тот же набор действий и порядок кнопок, что у ReportForm/NetworkReportForm.</summary>
    internal sealed class WatershedReportForm : Form
    {
        private readonly ListView m_List = new ListView();
        private readonly List<WatershedRow> m_Rows;

        // Строка -> примитив, из которого она построена: нужен профилю лога
        // (имя поверхности) - у WatershedResult его нет, только у сущности.
        private readonly Dictionary<WatershedRow, DwgWatershed> m_Owners = new Dictionary<WatershedRow, DwgWatershed>();

        /// <summary>Положить таблицу на план.</summary>
        internal Action<List<WatershedRow>> PlaceTable;

        internal WatershedReportForm(List<DwgWatershed> entities)
        {
            var results = new List<WatershedResult>(entities.Count);
            foreach (var e in entities) results.Add(e.Snapshot);
            m_Rows = WatershedReport.Build(results);

            foreach (var r in m_Rows)
            {
                foreach (var e in entities)
                    if (ReferenceEquals(e.Snapshot, r.Source)) { m_Owners[r] = e; break; }
            }

            Text = "Ведомость водосборов";
            Font = new Font("Segoe UI", 9f);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(980, 480);
            Icon = AbrIcon.Create();

            m_List.Dock = DockStyle.Fill;
            m_List.View = View.Details;
            m_List.FullRowSelect = true;
            m_List.GridLines = true;
            foreach (var h in WatershedReport.Header())
                m_List.Columns.Add(h, h.Length > 10 ? 130 : 90);

            foreach (var r in m_Rows)
            {
                var cells = WatershedReport.ToCells(r);
                var item = new ListViewItem(cells[0]);
                for (int i = 1; i < cells.Length; i++) item.SubItems.Add(cells[i]);
                if (r.IsProblem) item.ForeColor = Color.FromArgb(0xD9, 0x3A, 0x2B);
                item.Tag = r;
                m_List.Items.Add(item);
            }
            Controls.Add(m_List);

            var sep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(220, 220, 220) };
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 44 };

            var toPlan = UiTheme.MakeButton("Таблица на план", BtnKind.Ghost, 140, 28);
            var csv = UiTheme.MakeButton("Экспорт CSV", BtnKind.Ghost, 110, 28);
            var html = UiTheme.MakeButton("Экспорт HTML", BtnKind.Ghost, 120, 28);
            var profile = UiTheme.MakeButton("Профиль лога в CSV", BtnKind.Ghost, 160, 28);
            var close = UiTheme.MakeButton("Закрыть", BtnKind.Primary, 90, 28);

            bottom.Width = ClientSize.Width;   // якоря Right ставить ТОЛЬКО после Width
            toPlan.Location = new Point(12, 8);
            csv.Location = new Point(158, 8);
            html.Location = new Point(274, 8);
            profile.Location = new Point(398, 8);
            close.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            close.Location = new Point(bottom.Width - 100, 8);

            toPlan.Click += delegate { if (PlaceTable != null) { PlaceTable(m_Rows); Close(); } };
            csv.Click += delegate { SaveAs("CSV (*.csv)|*.csv", ".csv", WatershedReport.ToCsv(m_Rows)); };
            html.Click += delegate { SaveAs("HTML (*.html)|*.html", ".html", WatershedReport.ToHtml(m_Rows)); };
            profile.Click += OnExportProfile;
            close.Click += delegate { Close(); };

            bottom.Controls.Add(toPlan);
            bottom.Controls.Add(csv);
            bottom.Controls.Add(html);
            bottom.Controls.Add(profile);
            bottom.Controls.Add(close);
            Controls.Add(sep);
            Controls.Add(bottom);
        }

        private void OnExportProfile(object sender, EventArgs e)
        {
            if (m_List.SelectedItems.Count == 0)
            {
                MessageBox.Show(this, "Выберите строку водосбора в таблице.", "Профиль лога",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var row = (WatershedRow)m_List.SelectedItems[0].Tag;
            DwgWatershed owner;
            if (!m_Owners.TryGetValue(row, out owner) || owner.Snapshot == null)
            {
                MessageBox.Show(this, "Нет данных для профиля.", "Профиль лога",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var surfaceRef = FindSurface(owner.SurfaceName);
            var pathFromOutlet = new List<Pt>(owner.Snapshot.Path);
            pathFromOutlet.Reverse();   // Path хранится от истока к створу, профилю нужно от створа вверх

            // surfaceRef == null (поверхность закрыта) - LogProfile ловит исключение
            // от null.CreateSection и сам уходит на запасной путь из сохранённых отметок.
            var profile = LogProfile.Build(surfaceRef == null ? null : surfaceRef.Surface,
                                           pathFromOutlet, owner.Snapshot.PathElevations);

            if (profile.Count == 0)
            {
                MessageBox.Show(this, "Профиль пуст: поверхность закрыта, а сохранённых отметок нет " +
                                "(водосбор построен до внедрения профиля - пересчитайте его).",
                                "Профиль лога", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("Расстояние, м;Отметка, м");
            foreach (var p in profile)
                sb.AppendLine(p.Distance.ToString("F2", ci) + ";" + p.Z.ToString("F2", ci));

            SaveAs("CSV (*.csv)|*.csv", ".csv", sb.ToString());
        }

        private static SurfaceRef FindSurface(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var s in SurfaceAccess.GetSurfaces())
                if (string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) return s;
            return null;
        }

        private void SaveAs(string filter, string ext, string content)
        {
            using (var dlg = new SaveFileDialog { Filter = filter, FileName = "Ведомость водосборов" + ext })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                // UTF-8 с BOM: без него RU-Excel ломает кириллицу в CSV.
                File.WriteAllText(dlg.FileName, content, new UTF8Encoding(true));
            }
        }
    }
}
