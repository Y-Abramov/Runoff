using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Abr.Sdk;
using AbrRunoff.Core.Watershed;
using AbrRunoff.Robur;

namespace AbrRunoff.UI
{
    /// <summary>
    /// Состояние диалога, сохраняемое между переоткрытиями ради указания точки
    /// на плане (см. WatershedDialog.PickPointRequested). Пикать точку из уже
    /// открытого модального диалога ненадёжно - у Robur свой хозяин окна, и
    /// Hide()/EnableWindow-трюки не приживаются везде одинаково. Здесь диалог
    /// закрывается, команда пикает точку тем же приёмом, что и везде в модуле
    /// (CadCursors.GetPoint напрямую, без открытого диалога поверх), и открывает
    /// диалог заново с этим состоянием.
    /// </summary>
    internal sealed class WatershedDialogState
    {
        internal int SurfaceIndex;
        internal int DesignIndex;
        internal string StepText;
        internal List<bool> OutletChecked = new List<bool>();
        internal List<WatershedRequest> ManualOutlets = new List<WatershedRequest>();
    }

    /// <summary>Выбор поверхности, проектной поверхности, створов и шага для расчёта водосбора.</summary>
    internal sealed class WatershedDialog : Form
    {
        private readonly IList<SurfaceRef> m_Surfaces;
        private readonly WatershedSettings m_Settings;
        private readonly List<WatershedRequest> m_ManualOutlets;

        private readonly ComboBox m_SurfaceCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox m_DesignCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox m_Step = new TextBox();
        private readonly Label m_Cells = new Label();
        private readonly CheckedListBox m_Outlets = new CheckedListBox();

        // Параллельно m_Outlets.Items: тот же индекс -> точка створа.
        private readonly List<WatershedRequest> m_OutletData = new List<WatershedRequest>();

        internal List<WatershedBuildResult> Results { get; private set; }

        internal WatershedDialog(IList<SurfaceRef> surfaces, IList<CulvertRef> culverts,
                                 WatershedSettings defaults, WatershedDialogState state)
        {
            m_Surfaces = surfaces;
            m_Settings = defaults ?? new WatershedSettings();
            m_ManualOutlets = state != null ? state.ManualOutlets : new List<WatershedRequest>();
            Results = new List<WatershedBuildResult>();

            Text = "Водосбор";
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(460, 460);
            Icon = AbrIcon.Create();

            int y = 12;

            AddLabel("Поверхность:", 12, y); y += 20;
            FillSurfaces(m_SurfaceCombo, surfaces, includeNone: false);
            m_SurfaceCombo.Location = new Point(12, y);
            m_SurfaceCombo.Width = 436;
            m_SurfaceCombo.SelectedIndexChanged += delegate { UpdateEstimate(); };
            Controls.Add(m_SurfaceCombo);
            y += 30;

            AddLabel("Проектная поверхность (насыпь врезается в рельеф, необязательно):", 12, y);
            y += 20;
            FillSurfaces(m_DesignCombo, surfaces, includeNone: true);
            m_DesignCombo.Location = new Point(12, y);
            m_DesignCombo.Width = 436;
            Controls.Add(m_DesignCombo);
            y += 34;

            AddLabel("Шаг сетки, м:", 12, y);
            m_Step.Location = new Point(120, y - 3);
            m_Step.Width = 70;
            m_Step.TextChanged += delegate { UpdateEstimate(); };
            Controls.Add(m_Step);

            m_Cells.Location = new Point(200, y);
            m_Cells.AutoSize = false;
            m_Cells.Size = new Size(248, 20);
            Controls.Add(m_Cells);
            y += 30;

            AddLabel("Замыкающие створы:", 12, y); y += 20;
            foreach (var c in culverts)
            {
                var req = new WatershedRequest { OutletName = c.Name, X = c.Upper.X, Y = c.Upper.Y };
                m_OutletData.Add(req);
            }
            foreach (var req in m_ManualOutlets) m_OutletData.Add(req);

            for (int i = 0; i < m_OutletData.Count; i++)
            {
                bool isChecked = state == null || i >= state.OutletChecked.Count || state.OutletChecked[i];
                m_Outlets.Items.Add(FormatOutlet(m_OutletData[i]), isChecked);
            }
            m_Outlets.Location = new Point(12, y);
            m_Outlets.Size = new Size(436, 180);
            m_Outlets.CheckOnClick = true;
            Controls.Add(m_Outlets);
            y += 186;

            var pick = UiTheme.MakeButton("Указать точку на плане", BtnKind.Secondary, 200, 26);
            pick.Location = new Point(12, y);
            pick.Click += delegate { DialogResult = DialogResult.Retry; };
            Controls.Add(pick);

            var sep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(220, 220, 220) };
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 44 };

            var ok = UiTheme.MakeButton("Построить", BtnKind.Primary, 100, 28);
            var cancel = UiTheme.MakeButton("Отмена", BtnKind.Ghost, 90, 28);

            bottom.Width = ClientSize.Width;
            ok.Anchor = cancel.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            ok.Location = new Point(bottom.Width - 206, 8);
            cancel.Location = new Point(bottom.Width - 100, 8);

            ok.Click += OnBuild;
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; };

            bottom.Controls.Add(ok);
            bottom.Controls.Add(cancel);
            Controls.Add(sep);
            Controls.Add(bottom);
            AcceptButton = ok;
            CancelButton = cancel;

            if (surfaces.Count > 0)
            {
                m_SurfaceCombo.SelectedIndex = state != null && state.SurfaceIndex < surfaces.Count ? state.SurfaceIndex : 0;
                if (state == null)
                {
                    double step = SurfaceReader.SuggestStep(surfaces[0], m_Settings);
                    m_Step.Text = step.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            m_DesignCombo.SelectedIndex = state != null && state.DesignIndex >= 0 && state.DesignIndex <= surfaces.Count
                ? state.DesignIndex : 0;
            if (state != null && !string.IsNullOrEmpty(state.StepText)) m_Step.Text = state.StepText;
            UpdateEstimate();
        }

        /// <summary>Снимок состояния перед закрытием ради указания точки - см. WatershedDialogState.</summary>
        internal WatershedDialogState CaptureState()
        {
            var state = new WatershedDialogState
            {
                SurfaceIndex = m_SurfaceCombo.SelectedIndex,
                DesignIndex = m_DesignCombo.SelectedIndex,
                StepText = m_Step.Text,
                ManualOutlets = m_ManualOutlets
            };
            for (int i = 0; i < m_Outlets.Items.Count; i++)
                state.OutletChecked.Add(m_Outlets.GetItemChecked(i));
            return state;
        }

        private void AddLabel(string text, int x, int y)
        {
            Controls.Add(new Label { Text = text, AutoSize = true, Location = new Point(x, y) });
        }

        private static void FillSurfaces(ComboBox combo, IList<SurfaceRef> surfaces, bool includeNone)
        {
            combo.Items.Clear();
            if (includeNone) combo.Items.Add("(нет)");
            foreach (var s in surfaces)
                combo.Items.Add(s.IsDesigned ? s.Name + " (проектная)" : s.Name);
        }

        private static string FormatOutlet(WatershedRequest r)
        {
            return r.OutletName + "  [" + r.X.ToString("0.#") + ", " + r.Y.ToString("0.#") + "]";
        }

        private SurfaceRef SelectedSurface()
        {
            int i = m_SurfaceCombo.SelectedIndex;
            return i >= 0 && i < m_Surfaces.Count ? m_Surfaces[i] : null;
        }

        /// <summary>Индекс в m_Surfaces: первый элемент combo - "(нет)", остальные сдвинуты на 1.</summary>
        private SurfaceRef SelectedDesign()
        {
            int i = m_DesignCombo.SelectedIndex;
            return i <= 0 || i - 1 >= m_Surfaces.Count ? null : m_Surfaces[i - 1];
        }

        private double ParsedStep()
        {
            double step;
            return double.TryParse(m_Step.Text, System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out step) && step > 0.0
                ? step : 5.0;
        }

        private void UpdateEstimate()
        {
            var s = SelectedSurface();
            if (s == null) { m_Cells.Text = ""; return; }

            double step = ParsedStep();
            double w = s.Bounds.Right - s.Bounds.Left;
            double h = s.Bounds.Top - s.Bounds.Bottom;
            double cells = (Math.Floor(w / step) + 1) * (Math.Floor(h / step) + 1);

            m_Cells.Text = "Ячеек: " + cells.ToString("N0");
            m_Cells.ForeColor = cells > m_Settings.MaxCells
                ? Color.FromArgb(0xD9, 0x3A, 0x2B)   // тот же красный, что RunoffStyle.Error
                : SystemColors.ControlText;
        }

        private void OnBuild(object sender, EventArgs e)
        {
            var surface = SelectedSurface();
            if (surface == null)
            {
                MessageBox.Show(this, "Выберите поверхность.", "Водосбор",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var outlets = new List<WatershedRequest>();
            for (int i = 0; i < m_Outlets.Items.Count; i++)
                if (m_Outlets.GetItemChecked(i)) outlets.Add(m_OutletData[i]);

            if (outlets.Count == 0)
            {
                MessageBox.Show(this, "Не отмечено ни одного створа. Отметьте трубу в списке либо " +
                                "укажите точку на плане.", "Водосбор",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var design = SelectedDesign();
            double step = ParsedStep();

            using (var progress = new WatershedProgressDialog())
            {
                progress.Show(this);
                List<WatershedBuildResult> built;
                try
                {
                    built = WatershedBuilder.Build(surface, design, step, m_Settings, outlets,
                        delegate (int done, int total, string stage) { progress.Report(done, total, stage); });
                }
                finally { progress.Close(); }

                if (progress.IsCancelled) return;

                Results = built;
            }

            var errors = new List<string>();
            foreach (var r in Results) if (r.Result == null) errors.Add(r.OutletName + ": " + r.Error);
            if (errors.Count > 0 && errors.Count == Results.Count)
            {
                MessageBox.Show(this, string.Join("\r\n", errors.ToArray()), "Водосбор",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
        }
    }
}
