using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Abr.Sdk;
using AbrRunoff.Core;
using AbrRunoff.Robur;

namespace AbrRunoff.UI
{
    internal sealed class RoadSelectDialog : Form
    {
        private readonly CheckedListBox m_List = new CheckedListBox();
        private readonly List<RoadAccess.RoadRef> m_Roads;
        private readonly bool[] m_HasDitch;

        internal RunoffSettings Settings { get; private set; }
        internal List<RoadAccess.RoadRef> Selected { get; private set; }

        internal RoadSelectDialog(List<RoadAccess.RoadRef> roads, RunoffSettings defaults)
        {
            m_Roads = roads;
            Settings = defaults.Clone();
            Selected = new List<RoadAccess.RoadRef>();

            Text = "Схема стока: выбор трасс";
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(440, 340);
            Icon = AbrIcon.Create();

            // Дешёвая проба ~20 станций: полное сэмплирование всех дорог повесило бы диалог.
            m_HasDitch = new bool[roads.Count];
            for (int i = 0; i < roads.Count; i++)
            {
                bool hasDitch = false;
                try { hasDitch = DitchReader.HasAnyDitch(roads[i].Road); }
                catch { }
                m_HasDitch[i] = hasDitch;
                // Вид пути в подписи: автодороги и ЖД пути лежат в одном списке,
                // по имени модели их не различить.
                string label = roads[i].Name + "  [" + roads[i].KindTitle + "]";
                m_List.Items.Add(hasDitch ? label : label + "  (кювет не найден)");
            }

            m_List.Location = new Point(12, 12);
            m_List.Size = new Size(416, 236);
            m_List.CheckOnClick = true;
            m_List.DrawMode = DrawMode.OwnerDrawFixed;
            m_List.DrawItem += OnDrawItem;
            // Снимаем галку с бескюветной строки сразу: погасить чекбокс штатно нельзя,
            // поэтому отменяем саму отметку.
            m_List.ItemCheck += delegate (object s, ItemCheckEventArgs e)
            {
                if (e.Index >= 0 && e.Index < m_HasDitch.Length && !m_HasDitch[e.Index])
                    e.NewValue = CheckState.Unchecked;
            };
            Controls.Add(m_List);

            var sep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(220, 220, 220) };
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 44 };

            var settingsBtn = UiTheme.MakeButton("Параметры...", BtnKind.Ghost, 110, 28);
            var ok = UiTheme.MakeButton("Построить", BtnKind.Primary, 100, 28);
            var cancel = UiTheme.MakeButton("Отмена", BtnKind.Ghost, 90, 28);

            bottom.Width = ClientSize.Width;
            settingsBtn.Location = new Point(12, 8);
            ok.Anchor = cancel.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            ok.Location = new Point(bottom.Width - 206, 8);
            cancel.Location = new Point(bottom.Width - 100, 8);

            settingsBtn.Click += delegate
            {
                using (var dlg = new SchemeSettingsDialog(Settings))
                    if (dlg.ShowDialog(this) == DialogResult.OK) Settings = dlg.Value;
            };
            ok.Click += delegate
            {
                Selected.Clear();
                for (int i = 0; i < m_Roads.Count; i++)
                    if (m_List.GetItemChecked(i)) Selected.Add(m_Roads[i]);
                if (Selected.Count == 0)
                {
                    MessageBox.Show(this, "Не отмечено ни одной дороги.", "Схема стока",
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                DialogResult = DialogResult.OK;
            };
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; };

            bottom.Controls.Add(settingsBtn);
            bottom.Controls.Add(ok);
            bottom.Controls.Add(cancel);
            Controls.Add(sep);
            Controls.Add(bottom);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        private void OnDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            e.DrawBackground();
            bool enabled = m_HasDitch[e.Index];
            var color = enabled ? SystemColors.WindowText : SystemColors.GrayText;
            TextRenderer.DrawText(e.Graphics, m_List.Items[e.Index].ToString(), e.Font,
                                  new Rectangle(e.Bounds.X + 18, e.Bounds.Y, e.Bounds.Width - 18, e.Bounds.Height),
                                  color, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            ControlPaint.DrawCheckBox(e.Graphics,
                new Rectangle(e.Bounds.X + 1, e.Bounds.Y + 1, 14, 14),
                (m_List.GetItemChecked(e.Index) ? ButtonState.Checked : ButtonState.Normal) |
                (enabled ? ButtonState.Normal : ButtonState.Inactive));
            e.DrawFocusRectangle();
        }
    }
}
