using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileSigner321
{
    internal sealed class MainForm : Form
    {
        private const string Product = "321.su File Signer";
        private const string Website = "https://321.su";
        private const string Support = "support@321.su";

        private readonly ComboBox certificatePicker = new ComboBox();
        private readonly Button createCertificate = MakeButton(UiText.Get("Создать сертификат"));
        private readonly Button exportCertificate = MakeButton(UiText.Get("Экспорт .cer"));
        private readonly Button exportBackup = MakeButton(UiText.Get("Резервная копия .pfx"));
        private readonly Button addFiles = MakeButton(UiText.Get("Добавить файлы…"));
        private readonly Button removeFiles = MakeButton(UiText.Get("Убрать выбранные"));
        private readonly Button sign = MakeButton(UiText.Get("Подписать копии"));
        private readonly ListView files = new ListView();
        private readonly LinkLabel about = new LinkLabel();
        private readonly Label certificateLabel = new Label();
        private readonly ToolStripButton russian = new ToolStripButton("RU") { Checked = true };
        private readonly ToolStripButton english = new ToolStripButton("EN");
        private readonly Dictionary<ListViewItem, Action> rowText = new Dictionary<ListViewItem, Action>();
        private Func<string> renderStatus = () => UiText.Get("Перетащи файлы в список или нажми «Добавить файлы».");
        private readonly ToolStripStatusLabel statusText = new ToolStripStatusLabel();
        private readonly ToolTip tips = new ToolTip();
        private List<SigningCertificate> certificates = new List<SigningCertificate>();
        private bool signing;

        public MainForm()
        {
            Text = Product;
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1000, 568);
            MinimumSize = new Size(770, 440);
            Font = new Font("Segoe UI", 10F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.White;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6,
                Padding = new Padding(18, 13, 18, 10)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(layout);

            var header = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
            header.Controls.Add(new Label
            {
                Text = Product, AutoSize = true, Font = new Font("Segoe UI Semibold", 19F),
                ForeColor = Color.FromArgb(25, 49, 75), Margin = new Padding(0, 0, 12, 0)
            });
            about.AutoSize = true;
            about.Margin = new Padding(3, 12, 0, 0);
            about.LinkClicked += (sender, args) => ShowAbout();
            header.Controls.Add(about);
            layout.Controls.Add(header, 0, 0);
            certificateLabel.AutoSize = true;
            certificateLabel.Margin = new Padding(3, 12, 0, 4);
            layout.Controls.Add(certificateLabel, 0, 1);

            var certificateRow = new TableLayoutPanel { ColumnCount = 4, AutoSize = true, Dock = DockStyle.Top };
            certificateRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            certificateRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            certificateRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            certificateRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            certificatePicker.Dock = DockStyle.Fill;
            certificatePicker.DropDownStyle = ComboBoxStyle.DropDownList;
            certificatePicker.AccessibleName = UiText.Get("Сертификат подписи");
            certificatePicker.DropDownWidth = 680;
            certificatePicker.SelectedIndexChanged += (sender, args) => UpdateControls();
            certificateRow.Controls.Add(certificatePicker, 0, 0);
            certificateRow.Controls.Add(createCertificate, 1, 0);
            certificateRow.Controls.Add(exportCertificate, 2, 0);
            certificateRow.Controls.Add(exportBackup, 3, 0);
            layout.Controls.Add(certificateRow, 0, 2);

            files.Dock = DockStyle.Fill;
            files.View = View.Details;
            files.FullRowSelect = true;
            files.AllowDrop = true;
            files.ShowItemToolTips = true;
            files.HideSelection = false;
            files.Margin = new Padding(3, 10, 3, 7);
            files.Columns.Add(UiText.Get("Файл"), 255);
            files.Columns.Add(UiText.Get("Папка"), 310);
            files.Columns.Add(UiText.Get("Результат"), 340);
            files.DragEnter += (sender, args) =>
            {
                if (!signing && args.Data.GetDataPresent(DataFormats.FileDrop)) args.Effect = DragDropEffects.Copy;
            };
            files.DragDrop += (sender, args) =>
            {
                var paths = args.Data.GetData(DataFormats.FileDrop) as string[];
                if (!signing && paths != null) AddPaths(paths);
            };
            files.SelectedIndexChanged += (sender, args) => UpdateControls();
            files.DoubleClick += (sender, args) => RevealSignedFile();
            layout.Controls.Add(files, 0, 3);

            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false };
            sign.BackColor = Color.FromArgb(225, 239, 251);
            sign.ForeColor = Color.FromArgb(22, 64, 104);
            sign.FlatStyle = FlatStyle.Flat;
            sign.FlatAppearance.BorderColor = Color.FromArgb(126, 171, 209);
            sign.FlatAppearance.MouseOverBackColor = Color.FromArgb(211, 231, 248);
            sign.FlatAppearance.MouseDownBackColor = Color.FromArgb(194, 221, 244);
            buttons.Controls.AddRange(new Control[] { addFiles, removeFiles, sign });
            layout.Controls.Add(buttons, 0, 4);

            var statusBar = new StatusStrip { Dock = DockStyle.Fill, BackColor = Color.White, SizingGrip = false };
            statusText.Spring = true;
            statusText.TextAlign = ContentAlignment.MiddleLeft;
            russian.Click += (sender, args) => SetLanguage(false);
            english.Click += (sender, args) => SetLanguage(true);
            statusBar.Items.AddRange(new ToolStripItem[] { statusText, russian, english });
            layout.Controls.Add(statusBar, 0, 5);

            addFiles.Click += (sender, args) => SelectFiles();
            removeFiles.Click += (sender, args) =>
            {
                foreach (ListViewItem item in files.SelectedItems.Cast<ListViewItem>().ToArray())
                {
                    rowText.Remove(item);
                    files.Items.Remove(item);
                }
                UpdateControls();
            };
            sign.Click += async (sender, args) => await SignFilesAsync();
            createCertificate.Click += (sender, args) => CreateCertificate();
            exportCertificate.Click += (sender, args) => ExportCertificate();
            exportBackup.Click += (sender, args) => ExportBackup();
            Shown += (sender, args) => RefreshCertificates(null);
            FormClosing += (sender, args) => { if (signing) args.Cancel = true; };
            SetLanguage(false);
        }

        private void SetLanguage(bool useEnglish)
        {
            UiText.English = useEnglish;
            russian.Checked = !useEnglish;
            english.Checked = useEnglish;
            about.Text = UiText.Get("О программе");
            certificateLabel.Text = UiText.Get("Сертификат подписи");
            certificatePicker.AccessibleName = certificateLabel.Text;
            createCertificate.Text = UiText.Get("Создать сертификат");
            exportCertificate.Text = UiText.Get("Экспорт .cer");
            exportBackup.Text = UiText.Get("Резервная копия .pfx");
            addFiles.Text = UiText.Get("Добавить файлы…");
            removeFiles.Text = UiText.Get("Убрать выбранные");
            sign.Text = UiText.Get("Подписать копии");
            files.Columns[0].Text = UiText.Get("Файл");
            files.Columns[1].Text = UiText.Get("Папка");
            files.Columns[2].Text = UiText.Get("Результат");
            int selection = certificatePicker.SelectedIndex;
            certificatePicker.BeginUpdate();
            try
            {
                certificatePicker.Items.Clear();
                foreach (var item in certificates) certificatePicker.Items.Add(item);
                certificatePicker.SelectedIndex = selection;
            }
            finally { certificatePicker.EndUpdate(); }
            foreach (var render in rowText.Values) render();
            statusText.Text = renderStatus();
            UpdateControls();
        }

        private void SetStatus(Func<string> render)
        {
            renderStatus = render;
            statusText.Text = render();
        }

        private void SetRow(ListViewItem item, Func<string> render, Func<string> tooltip = null)
        {
            Action update = () =>
            {
                item.SubItems[2].Text = render();
                item.ToolTipText = tooltip == null ? item.SubItems[2].Text : tooltip();
            };
            rowText[item] = update;
            update();
        }

        private static Button MakeButton(string text)
        {
            return new Button { Text = text, AutoSize = true, Padding = new Padding(7, 2, 7, 2) };
        }

        private void RefreshCertificates(string preferredThumbprint)
        {
            try
            {
                List<SigningCertificate> fresh = Certificates.ReadStores();
                var previous = certificates;
                certificates = fresh;
                certificatePicker.BeginUpdate();
                try
                {
                    certificatePicker.Items.Clear();
                    foreach (var certificate in fresh) certificatePicker.Items.Add(certificate);
                    int index = fresh.FindIndex(item => item.Certificate.Thumbprint == preferredThumbprint);
                    certificatePicker.SelectedIndex = index >= 0 ? index : fresh.Count > 0 ? 0 : -1;
                }
                finally
                {
                    certificatePicker.EndUpdate();
                    foreach (var item in previous) item.Dispose();
                }
            }
            catch (Exception error) { ShowError(error); }
            UpdateControls();
        }

        private void UpdateControls()
        {
            bool selected = certificatePicker.SelectedItem is SigningCertificate;
            sign.Enabled = !signing && selected && files.Items.Count > 0;
            addFiles.Enabled = !signing;
            removeFiles.Enabled = !signing && files.SelectedItems.Count > 0;
            createCertificate.Enabled = !signing;
            exportCertificate.Enabled = !signing && selected;
            exportBackup.Enabled = !signing && selected;
            certificatePicker.Enabled = !signing;
            var item = certificatePicker.SelectedItem as SigningCertificate;
            tips.SetToolTip(certificatePicker, item == null ? null :
                item.Certificate.Subject + Environment.NewLine + UiText.Get("Отпечаток: ") + item.Certificate.Thumbprint);
        }

        private void SelectFiles()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Multiselect = true;
                dialog.Filter = UiText.Get("Поддерживаемые файлы|*.ps1;*.psm1;*.psd1;*.exe;*.dll;*.msi;*.cab;*.cat;*.rdp|Все файлы|*.*");
                if (dialog.ShowDialog(this) == DialogResult.OK) AddPaths(dialog.FileNames);
            }
        }

        private void AddPaths(IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {
                if (!File.Exists(path)) continue;
                string full = Path.GetFullPath(path);
                if (files.Items.ContainsKey(full)) continue;
                var item = new ListViewItem(Path.GetFileName(full)) { Name = full };
                item.SubItems.Add(Path.GetDirectoryName(full));
                item.SubItems.Add("");
                SetRow(item, () => FileSigning.Supports(full) ? UiText.Get("Готов к подписи") : UiText.Get("Тип файла не поддерживается"));
                files.Items.Add(item);
            }
            UpdateControls();
        }

        private async Task SignFilesAsync()
        {
            var selected = certificatePicker.SelectedItem as SigningCertificate;
            if (selected == null || signing) return;

            signing = true;
            UpdateControls();
            int successCount = 0;
            int errorCount = 0;
            try
            {
                foreach (ListViewItem item in files.Items)
                {
                    item.Tag = null;
                    SetRow(item, () => UiText.Get("Подпись…"));
                    SetStatus(() => UiText.Get("Подпись: ") + item.Text);
                    string source = item.Name;
                    try
                    {
                        SignedFile result = await Task.Run(() => FileSigning.SignCopy(source, selected.Certificate));
                        item.Tag = result.Path;
                        SetRow(item, () => result.Status() + " | " + Path.GetFileName(result.Path),
                            () => result.Status() + Environment.NewLine + result.Path);
                        successCount++;
                    }
                    catch (Exception error)
                    {
                        SetRow(item, () => UiText.Get("Ошибка: ") + error.Message);
                        errorCount++;
                    }
                }
                SetStatus(() => string.Format(UiText.Get("Подписано: {0}. Ошибок: {1}."), successCount, errorCount));
            }
            finally
            {
                signing = false;
                UpdateControls();
            }
        }

        private void CreateCertificate()
        {
            using (var dialog = new CertificateDialog(Icon))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string thumbprint = Certificates.Create(dialog.CertificateName, dialog.ValidDays);
                    RefreshCertificates(thumbprint);
                    SetStatus(() => UiText.Get("Сертификат создан в личном хранилище. Автоматическое доверие не добавлено."));
                }
                catch (Exception error) { ShowError(error); }
            }
        }

        private void ExportCertificate()
        {
            var selected = certificatePicker.SelectedItem as SigningCertificate;
            if (selected == null) return;
            using (var dialog = new SaveFileDialog
            {
                Filter = UiText.Get("Публичный сертификат (*.cer)|*.cer"), FileName = selected.Certificate.Thumbprint + ".cer",
                AddExtension = true, OverwritePrompt = true
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    File.WriteAllBytes(dialog.FileName, selected.Certificate.Export(
                        System.Security.Cryptography.X509Certificates.X509ContentType.Cert));
                    string destination = dialog.FileName;
                    SetStatus(() => UiText.Get("Публичный сертификат сохранён: ") + destination);
                }
                catch (Exception error) { ShowError(error); }
            }
        }

        private void ExportBackup()
        {
            var selected = certificatePicker.SelectedItem as SigningCertificate;
            if (selected == null) return;
            using (var save = new SaveFileDialog
            {
                Filter = UiText.Get("Резервная копия сертификата (*.pfx)|*.pfx"),
                FileName = selected.Certificate.Thumbprint + ".pfx",
                DefaultExt = "pfx", AddExtension = true, OverwritePrompt = true
            })
            {
                if (save.ShowDialog(this) != DialogResult.OK) return;
                using (var dialog = new PfxPasswordDialog(Icon))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    try
                    {
                        using (var password = dialog.ReadPassword())
                            Certificates.ExportBackup(selected.Certificate, password, save.FileName);
                        string destination = save.FileName;
                        SetStatus(() => UiText.Get("Резервная копия .pfx сохранена: ") + destination);
                    }
                    catch (Exception error) { ShowError(error); }
                }
            }
        }

        private void RevealSignedFile()
        {
            if (files.SelectedItems.Count != 1) return;
            var path = files.SelectedItems[0].Tag as string;
            if (path != null && File.Exists(path))
                Open(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });
        }

        private void ShowAbout()
        {
            using (var about = new Form
            {
                Text = UiText.Get("О программе"), StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(430, 215),
                FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false,
                ShowInTaskbar = false, Font = Font, Icon = Icon
            })
            {
                var box = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                    WrapContents = false, Padding = new Padding(16)
                };
                var version = Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
                box.Controls.Add(new Label { Text = Product + " " + version, AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
                box.Controls.Add(new Label { Text = UiText.Get("Разработчик: 321.su"), AutoSize = true });
                box.Controls.Add(new Label { Text = "© 2026 321.su", AutoSize = true });
                var website = new LinkLabel { Text = Website, AutoSize = true };
                var support = new LinkLabel { Text = Support, AutoSize = true };
                website.LinkClicked += (s, e) => Open(new ProcessStartInfo(Website) { UseShellExecute = true });
                support.LinkClicked += (s, e) => Open(new ProcessStartInfo("mailto:" + Support) { UseShellExecute = true });
                box.Controls.Add(website);
                box.Controls.Add(support);
                about.Controls.Add(box);
                about.ShowDialog(this);
            }
        }

        private void Open(ProcessStartInfo target)
        {
            try { using (Process process = Process.Start(target)) { } }
            catch (Exception error) { ShowError(error); }
        }

        private void ShowError(Exception error)
        {
            MessageBox.Show(this, error.Message, Product, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var item in certificates) item.Dispose();
                tips.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
