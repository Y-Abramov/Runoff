using System;
using System.Drawing;
using System.Windows.Forms;
using Abr.Sdk;

namespace AbrRunoff.UI
{
    /// <summary>
    /// Прогресс расчёта водосбора с отменой. Схема как у DemLoader.Ui.ProgressDialog:
    /// работа идёт в потоке UI, окно оживает на Application.DoEvents внутри Report,
    /// фонового потока нет. Отмена не прерывает расчёт мгновенно - отмечается флагом,
    /// вызывающий код проверяет его ПОСЛЕ завершения работы и просто не размещает
    /// результат. Мгновенная отмена потребовала бы протаскивать токен через весь
    /// конвейер ядра - непропорционально для расчёта одной поверхности.
    /// </summary>
    internal sealed class WatershedProgressDialog : Form
    {
        private readonly ProgressBar m_Bar = new ProgressBar();
        private readonly Label m_Text = new Label();
        private readonly Button m_Cancel = new Button();

        internal bool IsCancelled { get; private set; }

        internal WatershedProgressDialog()
        {
            Text = "Расчёт водосбора";
            Icon = AbrIcon.Create();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ControlBox = false;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(380, 110);

            m_Text.Left = 12; m_Text.Top = 12; m_Text.Width = 356;
            m_Bar.Left = 12; m_Bar.Top = 38; m_Bar.Width = 356; m_Bar.Maximum = 1;

            m_Cancel.Text = "Отмена";
            m_Cancel.Left = 283; m_Cancel.Top = 72; m_Cancel.Width = 85;
            m_Cancel.Click += delegate
            {
                IsCancelled = true;
                m_Cancel.Enabled = false;
                m_Text.Text = "Отмена: ждём завершения текущего шага";
            };

            Controls.Add(m_Text);
            Controls.Add(m_Bar);
            Controls.Add(m_Cancel);
        }

        internal void Report(int done, int total, string stage)
        {
            if (IsCancelled) { Application.DoEvents(); return; }

            m_Bar.Maximum = Math.Max(1, total);
            m_Bar.Value = Math.Min(m_Bar.Maximum, Math.Max(0, done));
            m_Text.Text = stage + " (" + done + " из " + total + ")";
            Application.DoEvents();
        }
    }
}
