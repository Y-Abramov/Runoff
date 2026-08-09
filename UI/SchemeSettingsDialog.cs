using System;
using System.Drawing;
using System.Windows.Forms;
using Abr.Sdk;
using AbrRunoff.Core;

namespace AbrRunoff.UI
{
    internal sealed class SchemeSettingsDialog : Form
    {
        // Анализ
        private readonly NumericUpDown m_MinGrade = Num(0, 50, 1);
        private readonly NumericUpDown m_PlateauEps = Num(0, 5, 2);
        private readonly NumericUpDown m_PipeTol = Num(0, 200, 0);
        private readonly NumericUpDown m_SampleStep = Num(0.5m, 50, 1);

        // Оформление
        private readonly NumericUpDown m_ArrowStep = Num(1, 500, 0);
        private readonly NumericUpDown m_GlyphScale = Num(0.2m, 10, 2);
        private readonly NumericUpDown m_TextHeight = Num(0.2m, 20, 2);
        private readonly CheckBox m_PointLabels = new CheckBox { Text = "Подписи характерных точек", AutoSize = true };
        private readonly CheckBox m_GradeLabels = new CheckBox { Text = "Подписи уклона вдоль участков", AutoSize = true };

        internal RunoffSettings Value { get; private set; }

        internal SchemeSettingsDialog(RunoffSettings initial)
        {
            Value = (initial ?? new RunoffSettings()).Clone();

            Text = "Параметры схемы стока";
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(400, 456);
            Icon = AbrIcon.Create();

            // Разделение на «что считаем» и «как показываем»: правят их в разное
            // время и по разным поводам, мешать в одну кучу нельзя.
            // Названия строк - по СМЫСЛУ для проектировщика, а не по имени поля.
            // «Порог плато» и «шаг сэмплирования» - жаргон разработчика: юзер
            // спросил, что это значит, и был прав.
            var gAnalysis = MakeGroup("Анализ", 12, 12, 376, 152);
            AddRow(gAnalysis, 0, "Мин. допустимый уклон дна, промилле", m_MinGrade, (decimal)Value.MinGradePermille);
            AddRow(gAnalysis, 1, "Уклон, ниже которого вода стоит, промилле", m_PlateauEps, (decimal)Value.PlateauEpsPermille);
            AddRow(gAnalysis, 2, "Допуск поиска трубы у точки сбора, м", m_PipeTol, (decimal)Value.PipeTolerance);
            AddRow(gAnalysis, 3, "Шаг разбивки профиля, м", m_SampleStep, (decimal)Value.SampleStep);

            var gLook = MakeGroup("Оформление", 12, 176, 376, 216);
            AddRow(gLook, 0, "Шаг стрелок, м", m_ArrowStep, (decimal)Value.ArrowStep);
            AddRow(gLook, 1, "Масштаб знаков", m_GlyphScale, (decimal)Value.GlyphScale);
            AddRow(gLook, 2, "Высота подписей, м", m_TextHeight, (decimal)Value.TextHeight);

            m_PointLabels.Checked = Value.ShowPointLabels;
            m_GradeLabels.Checked = Value.ShowGradeLabels;
            m_PointLabels.Location = new Point(16, 116);
            m_GradeLabels.Location = new Point(16, 142);
            gLook.Controls.Add(m_PointLabels);
            gLook.Controls.Add(m_GradeLabels);

            Controls.Add(gAnalysis);
            Controls.Add(gLook);

            ExplainSettings();

            var sep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(220, 220, 220) };
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 44 };

            var reset = UiTheme.MakeButton("По умолчанию", BtnKind.Ghost, 120, 28);
            var ok = UiTheme.MakeButton("OK", BtnKind.Primary, 90, 28);
            var cancel = UiTheme.MakeButton("Отмена", BtnKind.Ghost, 90, 28);

            bottom.Width = ClientSize.Width;   // якоря Right ставить ТОЛЬКО после Width
            reset.Location = new Point(12, 8);
            ok.Anchor = cancel.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            ok.Location = new Point(bottom.Width - 202, 8);
            cancel.Location = new Point(bottom.Width - 102, 8);

            reset.Click += delegate { LoadFrom(new RunoffSettings()); };
            ok.Click += delegate { Apply(); DialogResult = DialogResult.OK; };
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; };

            bottom.Controls.Add(reset);
            bottom.Controls.Add(ok);
            bottom.Controls.Add(cancel);

            Controls.Add(sep);
            Controls.Add(bottom);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        /// <summary>
        /// Подсказки к параметрам. Название в строке отвечает «что это», подсказка -
        /// «зачем крутить и что будет». Без них два параметра читались как жаргон:
        /// юзер прямо спросил, что означают «порог плато» и «шаг сэмплирования».
        /// </summary>
        private void ExplainSettings()
        {
            var tip = new ToolTip { AutoPopDelay = 20000, InitialDelay = 300, ReshowDelay = 100 };

            tip.SetToolTip(m_MinGrade,
                "Норма продольного уклона дна кювета (СП 34.13330 - 5 промилле).\n" +
                "Участки положе помечаются жёлтым как «уклон ниже нормы»:\n" +
                "вода идёт, но медленно, кювет заиливается.");

            tip.SetToolTip(m_PlateauEps,
                "Ниже этого уклона считается, что вода СТОИТ - направления нет.\n" +
                "Такой участок помечается красным знаком «плато».\n" +
                "Нужен потому, что отметки дна всегда содержат мелкий шум:\n" +
                "без порога любые доли промилле выглядели бы как течение.");

            tip.SetToolTip(m_PipeTol,
                "Насколько далеко от точки сбора искать трубу по пикету.\n" +
                "Труба в этом допуске - точка сбора считается имеющей выпуск,\n" +
                "иначе это бессточное понижение (красное двойное кольцо).");

            tip.SetToolTip(m_SampleStep,
                "Через сколько метров считывать отметку дна с продольного профиля.\n" +
                "Мельче шаг - точнее ловятся короткие перегибы и водоразделы,\n" +
                "но дольше пересчёт. 5 м хватает для большинства проектов.");

            tip.SetToolTip(m_ArrowStep, "Через сколько метров ставить стрелку направления течения.");
            tip.SetToolTip(m_GlyphScale, "Множитель размера знаков: водоразделов, точек сбора, стрелок.");
            tip.SetToolTip(m_TextHeight, "Высота подписей в метрах плана.");
        }

        private void LoadFrom(RunoffSettings s)
        {
            m_MinGrade.Value   = Clamp(m_MinGrade, (decimal)s.MinGradePermille);
            m_PlateauEps.Value = Clamp(m_PlateauEps, (decimal)s.PlateauEpsPermille);
            m_PipeTol.Value    = Clamp(m_PipeTol, (decimal)s.PipeTolerance);
            m_SampleStep.Value = Clamp(m_SampleStep, (decimal)s.SampleStep);
            m_ArrowStep.Value  = Clamp(m_ArrowStep, (decimal)s.ArrowStep);
            m_GlyphScale.Value = Clamp(m_GlyphScale, (decimal)s.GlyphScale);
            m_TextHeight.Value = Clamp(m_TextHeight, (decimal)s.TextHeight);
            m_PointLabels.Checked  = s.ShowPointLabels;
            m_GradeLabels.Checked  = s.ShowGradeLabels;
        }

        private void Apply()
        {
            Value.MinGradePermille   = (double)m_MinGrade.Value;
            Value.PlateauEpsPermille = (double)m_PlateauEps.Value;
            Value.PipeTolerance      = (double)m_PipeTol.Value;
            Value.SampleStep         = (double)m_SampleStep.Value;
            Value.ArrowStep          = (double)m_ArrowStep.Value;
            Value.GlyphScale         = (double)m_GlyphScale.Value;
            Value.TextHeight         = (double)m_TextHeight.Value;
            Value.ShowPointLabels    = m_PointLabels.Checked;
            Value.ShowGradeLabels    = m_GradeLabels.Checked;
        }

        private static GroupBox MakeGroup(string caption, int x, int y, int w, int h)
        {
            return new GroupBox { Text = caption, Location = new Point(x, y), Size = new Size(w, h) };
        }

        private static void AddRow(Control host, int row, string caption, NumericUpDown box, decimal value)
        {
            var lbl = new Label
            {
                Text = caption, AutoSize = false,
                Location = new Point(16, 26 + row * 28), Size = new Size(250, 22),
                TextAlign = ContentAlignment.MiddleLeft
            };
            box.Location = new Point(272, 24 + row * 28);
            box.Width = 88;
            box.Value = Clamp(box, value);
            host.Controls.Add(lbl);
            host.Controls.Add(box);
        }

        private static decimal Clamp(NumericUpDown box, decimal v)
        {
            if (v < box.Minimum) return box.Minimum;
            if (v > box.Maximum) return box.Maximum;
            return v;
        }

        private static NumericUpDown Num(decimal min, decimal max, int decimals)
        {
            return new NumericUpDown
            {
                Minimum = min, Maximum = max, DecimalPlaces = decimals,
                Increment = decimals == 0 ? 1m : 0.1m
            };
        }
    }
}
