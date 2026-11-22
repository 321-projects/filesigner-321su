using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace FileSigner321
{
    internal sealed class CertificateDialog : Form
    {
        private readonly TextBox name = new TextBox();
        private readonly NumericUpDown days = new NumericUpDown();

        public string CertificateName { get { return name.Text.Trim(); } }
        public int ValidDays { get { return Decimal.ToInt32(days.Value); } }

        public CertificateDialog(Icon icon)
        {
            Text = UiText.Get("Создать сертификат подписи");
            Icon = icon;
            Font = new Font("Segoe UI", 10F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(440, 225);
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(14) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 6; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { Text = UiText.Get("Имя сертификата"), AutoSize = true }, 0, 0);
            name.Dock = DockStyle.Top;
            name.MaxLength = 64;
            name.Text = Environment.UserName + " Code Signing";
            name.AccessibleName = UiText.Get("Имя сертификата");
            layout.Controls.Add(name, 0, 1);
            layout.Controls.Add(new Label { Text = UiText.Get("Срок действия, дней"), AutoSize = true, Margin = new Padding(3, 8, 0, 0) }, 0, 2);
            days.Minimum = 1;
            days.Maximum = 36525;
            days.Width = 130;
            days.AccessibleName = UiText.Get("Срок действия, дней");
            var today = DateTime.Today;
            days.Value = (today.AddYears(10) - today).Days;
            layout.Controls.Add(days, 0, 3);
            layout.Controls.Add(new Label { Text = UiText.Get("Самоподписанный сертификат в личном хранилище."), AutoSize = true }, 0, 4);

            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = UiText.Get("Отмена"), AutoSize = true, DialogResult = DialogResult.Cancel };
            var create = new Button { Text = UiText.Get("Создать"), AutoSize = true };
            create.Click += (sender, args) =>
            {
                if (!Regex.IsMatch(CertificateName, @"^[\p{L}\p{N}][\p{L}\p{N} ._-]{0,63}$"))
                {
                    MessageBox.Show(this, UiText.Get("Введи имя из букв, цифр, пробелов или знаков . _ -."), Text,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    name.Focus();
                    return;
                }
                DialogResult = DialogResult.OK;
            };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(create);
            layout.Controls.Add(buttons, 0, 5);
            AcceptButton = create;
            CancelButton = cancel;
            Controls.Add(layout);
        }
    }
}
