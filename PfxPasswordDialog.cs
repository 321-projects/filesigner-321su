using System;
using System.Drawing;
using System.Security;
using System.Windows.Forms;

namespace FileSigner321
{
    internal sealed class PfxPasswordDialog : Form
    {
        private readonly TextBox password = new TextBox();
        private readonly TextBox confirmation = new TextBox();

        public PfxPasswordDialog(Icon icon)
        {
            Text = UiText.Get("Пароль резервной копии .pfx");
            Icon = icon;
            Font = new Font("Segoe UI", 10F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(438, 210);
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(14)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < layout.RowCount; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { Text = UiText.Get("Пароль для файла .pfx"), AutoSize = true }, 0, 0);
            AddPasswordBox(layout, password, 1, UiText.Get("Пароль для файла .pfx"));
            layout.Controls.Add(new Label
            {
                Text = UiText.Get("Повтори пароль"), AutoSize = true, Margin = new Padding(3, 10, 3, 0)
            }, 0, 2);
            AddPasswordBox(layout, confirmation, 3, UiText.Get("Повтори пароль"));

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft
            };
            var cancel = new Button { Text = UiText.Get("Отмена"), AutoSize = true, DialogResult = DialogResult.Cancel };
            var save = new Button { Text = UiText.Get("Сохранить .pfx"), AutoSize = true };
            save.Click += (sender, args) =>
            {
                if (string.IsNullOrWhiteSpace(password.Text) ||
                    !string.Equals(password.Text, confirmation.Text, StringComparison.Ordinal))
                {
                    MessageBox.Show(this, UiText.Get("Введи один и тот же непустой пароль дважды."), Text,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    confirmation.Focus();
                    return;
                }
                DialogResult = DialogResult.OK;
            };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(save);
            layout.Controls.Add(buttons, 0, 4);
            AcceptButton = save;
            CancelButton = cancel;
            Controls.Add(layout);
        }

        private static void AddPasswordBox(TableLayoutPanel layout, TextBox box, int row, string label)
        {
            box.UseSystemPasswordChar = true;
            box.Dock = DockStyle.Top;
            box.AccessibleName = label;
            layout.Controls.Add(box, 0, row);
        }

        public SecureString ReadPassword()
        {
            var result = new SecureString();
            foreach (char character in password.Text) result.AppendChar(character);
            result.MakeReadOnly();
            password.Clear();
            confirmation.Clear();
            return result;
        }
    }
}
