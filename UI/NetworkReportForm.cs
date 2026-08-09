using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Abr.Sdk;
using AbrRunoff.Report;

namespace AbrRunoff.UI
{
    /// <summary>
    /// Ведомость по бассейнам - близнец ReportForm для ведомости стока: тот же
    /// набор действий и тот же порядок кнопок. Две ведомости одного модуля не
    /// должны работать по-разному.
    ///
    /// Отличие одно и вынужденное: зум идёт по КООРДИНАТЕ, а не по пикету. У сети
    /// нет общего пикетажа - её элементы приходят из разных моделей.
    /// </summary>
    internal sealed class NetworkReportForm : Form
    {
        private readonly ListView m_List = new ListView();
        private readonly List<NetworkRow> m_Rows;

        /// <summary>Зум к цепочке: (начало, выпуск или null-признак).</summary>
        internal Action<NetworkRow> ZoomTo;

        /// <summary>Положить таблицу на план.</summary>
        internal Action<List<NetworkRow>> PlaceTable;

        internal NetworkReportForm(List<NetworkRow> rows)
        {
            m_Rows = rows;

            Text = "Ведомость по бассейнам";
            Font = new Font("Segoe UI", 9f);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(940, 480);
            Icon = AbrIcon.Create();

            m_List.Dock = DockStyle.Fill;
            m_List.View = View.Details;
            m_List.FullRowSelect = true;
            m_List.GridLines = true;
            foreach (var h in NetworkReport.Header())
                m_List.Columns.Add(h, h.Length > 10 ? 130 : 90);

            foreach (var r in rows)
            {
                var cells = NetworkReport.ToCells(r);
                var item = new ListViewItem(cells[0]);
                for (int i = 1; i < cells.Length; i++) item.SubItems.Add(cells[i]);
                if (r.IsProblem) item.ForeColor = Color.FromArgb(0xD9, 0x3A, 0x2B);
                item.Tag = r;
                m_List.Items.Add(item);
            }
            m_List.DoubleClick += delegate
            {
                if (m_List.SelectedItems.Count == 0 || ZoomTo == null) return;
                ZoomTo((NetworkRow)m_List.SelectedItems[0].Tag);
            };
            Controls.Add(m_List);

            var sep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(220, 220, 220) };
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 44 };

            var toPlan = UiTheme.MakeButton("Таблица на план", BtnKind.Ghost, 140, 28);
            var csv = UiTheme.MakeButton("Экспорт CSV", BtnKind.Ghost, 110, 28);
            var html = UiTheme.MakeButton("Экспорт HTML", BtnKind.Ghost, 120, 28);
            var close = UiTheme.MakeButton("Закрыть", BtnKind.Primary, 90, 28);

            bottom.Width = ClientSize.Width;   // якоря Right ставить ТОЛЬКО после Width
            toPlan.Location = new Point(12, 8);
            csv.Location = new Point(158, 8);
            html.Location = new Point(274, 8);
            close.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            close.Location = new Point(bottom.Width - 100, 8);

            toPlan.Click += delegate { if (PlaceTable != null) { PlaceTable(m_Rows); Close(); } };
            csv.Click += delegate { SaveAs("CSV (*.csv)|*.csv", ".csv", NetworkReport.ToCsv(m_Rows)); };
            html.Click += delegate { SaveAs("HTML (*.html)|*.html", ".html", NetworkReport.ToHtml(m_Rows)); };
            close.Click += delegate { Close(); };

            bottom.Controls.Add(toPlan);
            bottom.Controls.Add(csv);
            bottom.Controls.Add(html);
            bottom.Controls.Add(close);
            Controls.Add(sep);
            Controls.Add(bottom);
        }

        private void SaveAs(string filter, string ext, string content)
        {
            using (var dlg = new SaveFileDialog { Filter = filter, FileName = "Ведомость по бассейнам" + ext })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                // UTF-8 с BOM: без него RU-Excel ломает кириллицу в CSV.
                File.WriteAllText(dlg.FileName, content, new UTF8Encoding(true));
            }
        }
    }
}
