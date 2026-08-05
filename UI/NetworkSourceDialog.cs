using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Abr.Sdk;
using AbrRunoff.Core;

namespace AbrRunoff.UI
{
    /// <summary>Выбор моделей-источников для сети водоотвода.</summary>
    internal sealed class NetworkSourceDialog : Form
    {
        private readonly CheckedListBox m_Roads = new CheckedListBox();
        private readonly NumericUpDown m_Tolerance = new NumericUpDown
        { Minimum = 1, Maximum = 200, DecimalPlaces = 0, Value = 15 };

        internal List<string> SelectedRoads { get; private set; }
        internal double LinkTolerance { get { return (double)m_Tolerance.Value; } }
        internal RunoffSettings Settings { get; private set; }

        internal NetworkSourceDialog(IList<string> roadNames, RunoffSettings defaults)
        {
            SelectedRoads = new List<string>();
            Settings = defaults.Clone();

            Text = "Сеть водоотвода: источники";
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(460, 380);
            Icon = AbrIcon.Create();

            var lbl = new Label
            {
                Text = "Дороги, входящие в сеть:", AutoSize = true,
                Location = new Point(12, 12)
            };
            Controls.Add(lbl);

            foreach (var n in roadNames) m_Roads.Items.Add(n, true);
            m_Roads.Location = new Point(12, 34);
            m_Roads.Size = new Size(436, 240);
            m_Roads.CheckOnClick = true;
            Controls.Add(m_Roads);

            var tolLbl = new Label
            {
                Text = "Допуск сшивки, м:", AutoSize = false,
                Location = new Point(12, 288), Size = new Size(160, 22),
                TextAlign = ContentAlignment.MiddleLeft
            };
            m_Tolerance.Location = new Point(178, 286);
            m_Tolerance.Width = 80;
            m_Tolerance.Value = 15;
            Controls.Add(tolLbl);
            Controls.Add(m_Tolerance);

            var hint = new Label
            {
                Text = "Канавы модели Площадка добавляются командой «Указать линии».",
                AutoSize = false, ForeColor = SystemColors.GrayText,
                Location = new Point(12, 316), Size = new Size(436, 20)
            };
            Controls.Add(hint);

            var sep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(220, 220, 220) };
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 44 };

            var ok = UiTheme.MakeButton("Построить", BtnKind.Primary, 100, 28);
            var cancel = UiTheme.MakeButton("Отмена", BtnKind.Ghost, 90, 28);

            bottom.Width = ClientSize.Width;
            ok.Anchor = cancel.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            ok.Location = new Point(bottom.Width - 206, 8);
            cancel.Location = new Point(bottom.Width - 100, 8);

            ok.Click += delegate
            {
                SelectedRoads.Clear();
                for (int i = 0; i < m_Roads.Items.Count; i++)
                    if (m_Roads.GetItemChecked(i)) SelectedRoads.Add(m_Roads.Items[i].ToString());

                if (SelectedRoads.Count == 0)
                {
                    MessageBox.Show(this, "Не отмечено ни одной дороги.", "Сеть водоотвода",
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                DialogResult = DialogResult.OK;
            };
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; };

            bottom.Controls.Add(ok);
            bottom.Controls.Add(cancel);
            Controls.Add(sep);
            Controls.Add(bottom);
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}
