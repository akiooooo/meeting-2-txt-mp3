using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Meeting2Text
{
    internal sealed class MainWindow : Form
    {
        static readonly Color Ink = Color.FromArgb(27, 46, 51);
        static readonly Color Muted = Color.FromArgb(76, 96, 100);
        static readonly Color Teal = Color.FromArgb(14, 110, 101);
        static readonly Color Edge = Color.FromArgb(202, 216, 216);
        static readonly Color Paper = Color.FromArgb(245, 248, 247);
        readonly ListBox list = new ListBox();
        readonly Label empty = new Label(), count = new Label(), status = new Label();
        readonly TextBox folder = new TextBox();
        readonly CheckBox split = new CheckBox();
        readonly CheckBox saveMp3 = new CheckBox(), transcribe = new CheckBox();
        readonly ComboBox language = new ComboBox();
        readonly CheckBox mn = new CheckBox();
        readonly DateTimePicker mnDate = new DateTimePicker();
        readonly Button openMn;
        string lastMn;
        bool TextInput { get { return list.Items.Count == 1 && IsTranscript((string)list.Items[0]); } }
        static bool IsTranscript(string p) { return Path.GetExtension(p).Equals(".txt", StringComparison.OrdinalIgnoreCase); }
        readonly NumericUpDown minutes = new NumericUpDown();
        readonly Button add, up, down, remove, browse, run, open, openText;
        readonly ProgressBar bar = new ProgressBar();
        CancellationTokenSource cancel;
        string lastResult;
        bool closePending, hasExportStatus;

        static Label Caption(string text, int size, bool bold = false)
        {
            return new Label { Text = text, AutoSize = true, ForeColor = Ink,
                Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
                Anchor = AnchorStyles.Left, Margin = Padding.Empty };
        }
        static Button ButtonFor(string text, int width, bool primary = false)
        {
            var b = new Button { Text = text, Width = width, Height = 32, FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Teal : Color.White, ForeColor = primary ? Color.White : Ink,
                Margin = new Padding(0, 0, 8, 0), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderColor = primary ? Teal : Edge;
            b.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(10, 92, 84) : Color.FromArgb(232, 243, 240);
            return b;
        }
        static TableLayoutPanel Rows(params int[] heights)
        {
            var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = heights.Length,
                Margin = Padding.Empty, Padding = Padding.Empty };
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int h in heights) p.RowStyles.Add(h < 0 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.Absolute, h));
            return p;
        }
        static FlowLayoutPanel Line()
        {
            return new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, WrapContents = false };
        }
        static TableLayoutPanel Pair()
        {
            var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            p.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            return p;
        }

        public MainWindow()
        {
            Text = "Meeting2Text";
            Font = new Font("Segoe UI", 10);
            BackColor = Paper;
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            MinimumSize = new Size(710, 630);
            ClientSize = new Size(840, 682);
            StartPosition = FormStartPosition.CenterScreen;

            var root = Rows(88, -1, 92);
            Controls.Add(root);
            var header = Rows(47, 24);
            header.BackColor = Color.FromArgb(25, 57, 60);
            header.Padding = new Padding(86, 6, 24, 8);
            header.Paint += delegate(object sender, PaintEventArgs e)
            {
                float scale = e.Graphics.DpiX / 96f;
                e.Graphics.ScaleTransform(scale, scale);
                using (var pen = new Pen(Color.FromArgb(119, 224, 193), 4))
                {
                    pen.StartCap = pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    int[] heights = { 6, 14, 23, 11, 17, 5 };
                    for (int i = 0; i < heights.Length; i++)
                        e.Graphics.DrawLine(pen, 28 + i * 8, 43 - heights[i], 28 + i * 8, 43 + heights[i]);
                }
            };
            var title = Caption("Meeting2Text", 21, true);
            title.ForeColor = Color.White;
            var description = Caption("Из записи встречи — в текст с таймкодами и MP3", 10);
            description.ForeColor = Color.FromArgb(201, 224, 220);
            header.Controls.Add(title, 0, 0);
            header.Controls.Add(description, 0, 1);
            root.Controls.Add(header, 0, 0);

            var body = Rows(42, -1, 54, 30, 43, 84, 38);
            body.Padding = new Padding(24, 8, 24, 12);
            root.Controls.Add(body, 0, 1);
            var section = Pair();
            section.Controls.Add(Caption("Очередь файлов", 13, true), 0, 0);
            count.AutoSize = true;
            count.ForeColor = Muted;
            count.Anchor = AnchorStyles.Right;
            section.Controls.Add(count, 1, 0);
            body.Controls.Add(section, 0, 0);

            var frame = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), BackColor = Edge, Margin = Padding.Empty };
            list.Dock = DockStyle.Fill;
            list.IntegralHeight = false;
            list.BorderStyle = BorderStyle.None;
            list.SelectionMode = SelectionMode.MultiExtended;
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.ItemHeight = 36;
            list.AccessibleName = "Видео и аудио в порядке объединения";
            list.DrawItem += DrawFile;
            list.SelectedIndexChanged += delegate { UpdateControls(); };
            list.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Delete && remove.Enabled) RemoveSelected();
                if (e.Control && e.KeyCode == Keys.A)
                {
                    for (int i = 0; i < list.Items.Count; i++) list.SetSelected(i, true);
                    e.SuppressKeyPress = true;
                }
            };
            empty.Dock = DockStyle.Fill;
            empty.BackColor = Color.White;
            empty.ForeColor = Muted;
            empty.TextAlign = ContentAlignment.MiddleCenter;
            empty.Text = "Перетащите сюда запись или расшифровку TXT\nили нажмите, чтобы выбрать файлы";
            empty.Cursor = Cursors.Hand;
            empty.Click += delegate { if (cancel == null) PickVideos(); };
            frame.Controls.Add(list);
            frame.Controls.Add(empty);
            foreach (Control target in new Control[] { list, empty })
            {
                target.AllowDrop = true;
                target.DragEnter += delegate(object sender, DragEventArgs e)
                {
                    e.Effect = cancel == null && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
                };
                target.DragDrop += delegate(object sender, DragEventArgs e)
                {
                    if (cancel == null && e.Data.GetDataPresent(DataFormats.FileDrop))
                        AddVideos((string[])e.Data.GetData(DataFormats.FileDrop));
                };
            }
            body.Controls.Add(frame, 0, 1);
            var commands = Line();
            commands.Padding = new Padding(0, 8, 0, 0);
            add = ButtonFor("Добавить файлы…", 152);
            up = ButtonFor("↑  Выше", 92);
            down = ButtonFor("↓  Ниже", 92);
            remove = ButtonFor("Убрать", 90);
            add.Click += delegate { PickVideos(); };
            up.Click += delegate { MoveSelected(-1); };
            down.Click += delegate { MoveSelected(1); };
            remove.Click += delegate { RemoveSelected(); };
            commands.Controls.AddRange(new Control[] { add, up, down, remove });
            body.Controls.Add(commands, 0, 2);
            body.Controls.Add(Caption("Папка сохранения", 12, true), 0, 3);
            var pathLine = Pair();
            folder.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            folder.Margin = new Padding(0, 0, 10, 0);
            folder.AccessibleName = "Папка сохранения";
            folder.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            browse = ButtonFor("Обзор…", 93);
            browse.Anchor = AnchorStyles.Right;
            browse.Margin = Padding.Empty;
            browse.Click += delegate
            {
                using (var dlg = new FolderBrowserDialog { Description = "Выберите папку для результата", SelectedPath = folder.Text })
                    if (dlg.ShowDialog(this) == DialogResult.OK) folder.Text = dlg.SelectedPath;
            };
            pathLine.Controls.Add(folder, 0, 0);
            pathLine.Controls.Add(browse, 1, 0);
            body.Controls.Add(pathLine, 0, 4);
            var outputs = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            outputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            outputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            var audioCard = Rows(33, 33);
            var textCard = Rows(33, 33);
            audioCard.BackColor = textCard.BackColor = Color.White;
            audioCard.Padding = textCard.Padding = new Padding(12, 5, 8, 5);
            audioCard.Margin = new Padding(0, 4, 5, 0);
            textCard.Margin = new Padding(5, 4, 0, 0);
            outputs.Controls.Add(audioCard, 0, 0);
            outputs.Controls.Add(textCard, 1, 0);
            saveMp3.Text = "Сохранить MP3";
            saveMp3.AutoSize = transcribe.AutoSize = true;
            saveMp3.Checked = true;
            transcribe.Text = "Расшифровка в TXT";
            saveMp3.Anchor = transcribe.Anchor = AnchorStyles.Left;
            saveMp3.Margin = transcribe.Margin = Padding.Empty;
            saveMp3.Font = transcribe.Font = new Font(Font, FontStyle.Bold);
            var langLabel = Caption("Язык речи", 10);
            langLabel.Margin = new Padding(3, 7, 6, 0);
            langLabel.Anchor = AnchorStyles.Top;
            language.AccessibleName = "Язык речи";
            language.DropDownStyle = ComboBoxStyle.DropDownList;
            language.Width = 110;
            language.Items.AddRange(new object[] { "Русский", "Авто", "Английский" });
            language.SelectedIndex = 0;
            var languages = Line();
            languages.Controls.AddRange(new Control[] { langLabel, language });
            audioCard.Controls.Add(saveMp3, 0, 0);
            textCard.Controls.Add(transcribe, 0, 0);
            textCard.Controls.Add(languages, 0, 1);
            body.Controls.Add(outputs, 0, 5);
            var settings = Line();
            split.AutoSize = true;
            split.Text = "Части до";
            split.Margin = new Padding(0, 4, 9, 0);
            minutes.Minimum = 1;
            minutes.Maximum = 1440;
            minutes.Value = 13;
            minutes.AccessibleName = "Максимальная длительность части в минутах";
            minutes.Width = 68;
            minutes.Margin = new Padding(0, 2, 6, 0);
            var unit = Caption("мин", 10);
            unit.Anchor = AnchorStyles.Top;
            unit.Margin = new Padding(0, 4, 0, 0);
            settings.Controls.AddRange(new Control[] { split, minutes, unit });
            audioCard.Controls.Add(settings, 0, 1);

            var notesRow = Line();
            mn.Text = "Протокол MN";
            mn.AutoSize = true;
            mn.Margin = new Padding(8, 8, 18, 0);
            var dateLabel = Caption("Дата встречи", 10);
            dateLabel.Margin = new Padding(0, 8, 8, 0);
            mnDate.Format = DateTimePickerFormat.Custom;
            mnDate.CustomFormat = "dd.MM.yyyy";
            mnDate.Width = 122;
            mnDate.AccessibleName = "Дата встречи для протокола MN";
            mnDate.Margin = new Padding(0, 5, 0, 0);
            var offline = new LinkLabel { Text = "Сборка с моделью", AutoSize = true, Margin = new Padding(10, 8, 0, 0) };
            offline.LinkClicked += delegate { Process.Start("https://github.com/akiooooo/meeting-2-txt-mp3/releases/latest/download/Meeting2Text-Offline-MN-x64.exe"); };
            notesRow.Controls.AddRange(new Control[] { mn, dateLabel, mnDate, offline });
            body.Controls.Add(notesRow, 0, 6);
            var footer = Rows(24, 6, -1);
            footer.Padding = new Padding(24, 6, 24, 8);
            footer.BackColor = Color.White;
            root.Controls.Add(footer, 0, 2);
            status.Dock = DockStyle.Fill;
            status.AutoEllipsis = true;
            status.ForeColor = Muted;
            status.Text = "Добавьте запись или расшифровку TXT";
            status.Margin = Padding.Empty;
            footer.Controls.Add(status, 0, 0);
            bar.Dock = DockStyle.Fill;
            bar.Margin = Padding.Empty;
            footer.Controls.Add(bar, 0, 1);
            var actions = Pair();
            run = ButtonFor("Создать MP3", 148, true);
            run.Anchor = AnchorStyles.Right;
            run.Margin = Padding.Empty;
            run.Click += async delegate { if (cancel != null) CancelExport(); else await Export(); };
            open = ButtonFor("Открыть папку", 135);
            open.Anchor = AnchorStyles.Left;
            open.Visible = false;
            open.Click += delegate
            {
                if (lastResult != null && Directory.Exists(Path.GetDirectoryName(lastResult)))
                    Process.Start("explorer.exe", "\"" + Path.GetDirectoryName(lastResult) + "\"");
            };
            openText = ButtonFor("Открыть текст", 135);
            openText.Visible = false;
            openText.Click += delegate
            {
                string path = Path.ChangeExtension(lastResult, ".txt");
                if (File.Exists(path)) Process.Start("notepad.exe", "\"" + path + "\"");
            };
            var results = Line();
            results.Padding = new Padding(0, 8, 0, 0);
            openMn = ButtonFor("Открыть MN", 115);
            openMn.Click += delegate { if (File.Exists(lastMn)) Process.Start("notepad.exe", "\"" + lastMn + "\""); };
            results.Controls.AddRange(new Control[] { open, openText, openMn });
            actions.Controls.Add(results, 0, 0);
            actions.Controls.Add(run, 1, 0);
            footer.Controls.Add(actions, 0, 2);
            mn.CheckedChanged += delegate { SettingsChanged(); };
            mnDate.ValueChanged += delegate { SettingsChanged(); };
            saveMp3.CheckedChanged += delegate { SettingsChanged(); };
            transcribe.CheckedChanged += delegate { SettingsChanged(); };
            language.SelectedIndexChanged += delegate { SettingsChanged(); };
            split.CheckedChanged += delegate { SettingsChanged(); };
            minutes.ValueChanged += delegate { SettingsChanged(); };
            folder.TextChanged += delegate { SettingsChanged(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (cancel != null) { e.Cancel = true; closePending = true; CancelExport(); }
            };
            UpdateControls();
        }

        void DrawFile(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            var r = e.Bounds;
            using (var brush = new SolidBrush(selected ? Color.FromArgb(216, 238, 233) : Color.White))
                e.Graphics.FillRectangle(brush, r);
            const TextFormatFlags format = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(e.Graphics, (e.Index + 1).ToString("00"), Font,
                new Rectangle(8, r.Y, 40, r.Height), Teal, format);
            TextRenderer.DrawText(e.Graphics, Path.GetFileNameWithoutExtension((string)list.Items[e.Index]), Font,
                new Rectangle(50, r.Y, Math.Max(1, r.Width - 130), r.Height), Ink, format);
            string extension = Path.GetExtension((string)list.Items[e.Index]).TrimStart('.').ToUpperInvariant();
            TextRenderer.DrawText(e.Graphics, extension, Font, new Rectangle(r.Right - 76, r.Y, 62, r.Height),
                Muted, format | TextFormatFlags.Right);
            if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
        }
        void PickVideos()
        {
            using (var dlg = new OpenFileDialog { Multiselect = true, Title = "Выберите запись или готовую расшифровку TXT",
                Filter = "Записи и расшифровки|*.txt;*.mp4;*.webm;*.mkv;*.mov;*.avi;*.m4v;*.mpeg;*.mpg;*.wmv;*.ts;*.mts;*.m2ts;*.flv;*.3gp;*.vob;*.ogv;*.asf;*.mxf;*.mp3;*.m4a;*.wav;*.flac;*.aac;*.ogg;*.opus|Все файлы|*.*" })
                if (dlg.ShowDialog(this) == DialogResult.OK) AddVideos(dlg.FileNames);
        }
        protected override bool ProcessCmdKey(ref Message message, Keys key)
        {
            if (cancel == null && key == (Keys.Control | Keys.O)) { PickVideos(); return true; }
            if (list.Focused && key == (Keys.Alt | Keys.Up) && up.Enabled) { MoveSelected(-1); return true; }
            if (list.Focused && key == (Keys.Alt | Keys.Down) && down.Enabled) { MoveSelected(1); return true; }
            return base.ProcessCmdKey(ref message, key);
        }
        void AddVideos(string[] paths)
        {
            paths = paths.Where(File.Exists).ToArray();
            if (paths.Length == 0) return;
            if (TextInput || (paths.Any(IsTranscript) && (paths.Length != 1 || list.Items.Count != 0)))
            {
                MessageBox.Show(this, "Для протокола из готового текста добавьте один TXT в пустую очередь.");
                return;
            }
            if (IsTranscript(paths[0])) mn.Checked = true;
            int first = list.Items.Count;
            foreach (string path in paths.Where(File.Exists)) list.Items.Add(path);
            if (list.Items.Count > first) list.SelectedIndex = first;
            SettingsChanged();
        }
        void RemoveSelected()
        {
            int first = list.SelectedIndex;
            foreach (int i in list.SelectedIndices.Cast<int>().Reverse().ToArray()) list.Items.RemoveAt(i);
            if (list.Items.Count > 0 && first >= 0) list.SelectedIndex = Math.Min(first, list.Items.Count - 1);
            SettingsChanged();
        }
        void MoveSelected(int delta)
        {
            int[] indexes = list.SelectedIndices.Cast<int>().ToArray();
            if (indexes.Length == 0 || indexes.First() + delta < 0 || indexes.Last() + delta >= list.Items.Count) return;
            if (delta > 0) Array.Reverse(indexes);
            list.BeginUpdate();
            list.ClearSelected();
            foreach (int i in indexes)
            {
                object value = list.Items[i];
                list.Items.RemoveAt(i);
                list.Items.Insert(i + delta, value);
            }
            foreach (int i in indexes) list.SetSelected(i + delta, true);
            list.TopIndex = Math.Min(list.TopIndex, list.SelectedIndex);
            list.EndUpdate();
            SettingsChanged();
        }
        void SettingsChanged()
        {
            if (cancel == null)
            {
                hasExportStatus = false;
                lastResult = null;
                bar.Value = 0;
            }
            UpdateControls();
        }
        void UpdateControls()
        {
            if (Disposing || IsDisposed) return;
            bool idle = cancel == null;
            bool selected = list.SelectedIndices.Count > 0;
            count.Text = "Файлов: " + list.Items.Count;
            empty.Visible = list.Items.Count == 0;
            if (empty.Visible) empty.BringToFront();
            add.Enabled = browse.Enabled = folder.Enabled = list.Enabled = saveMp3.Enabled = transcribe.Enabled = idle;
            saveMp3.Enabled = transcribe.Enabled = idle && !TextInput;
            mn.Enabled = idle && !TextInput;
            mnDate.Enabled = idle && mn.Checked;
            language.Enabled = idle && !TextInput && (transcribe.Checked || mn.Checked);
            split.Enabled = idle && !TextInput && saveMp3.Checked;
            remove.Enabled = idle && selected;
            up.Enabled = idle && selected && list.SelectedIndices[0] > 0;
            down.Enabled = idle && selected && list.SelectedIndices[list.SelectedIndices.Count - 1] < list.Items.Count - 1;
            minutes.Enabled = split.Enabled && split.Checked;
            bool outputChosen = mn.Checked || (!TextInput && (saveMp3.Checked || transcribe.Checked));
            run.Enabled = idle ? outputChosen && list.Items.Count > 0 && !String.IsNullOrWhiteSpace(folder.Text) : !cancel.IsCancellationRequested;
            run.Text = !idle ? "Отменить" : mn.Checked ? "Создать протокол" : !transcribe.Checked ? "Создать MP3" : saveMp3.Checked ? "Создать MP3 и TXT" : "Расшифровать";
            open.Visible = idle && lastResult != null;
            openText.Visible = open.Visible && transcribe.Checked && !TextInput;
            openMn.Visible = open.Visible && mn.Checked && lastMn != null;
            if (idle && lastResult == null && bar.Value == 0 && !hasExportStatus)
                status.Text = !outputChosen ? "Выберите MP3 или расшифровку" :
                    list.Items.Count == 0 ? "Добавьте запись или расшифровку TXT" : "Готово к обработке";
        }
        void CancelExport()
        {
            cancel.Cancel();
            run.Enabled = false;
            status.Text = "Отмена…";
        }
        async Task Export()
        {
            string[] files = list.Items.Cast<string>().ToArray();
            bool audio = !TextInput && saveMp3.Checked;
            bool txt = !TextInput && transcribe.Checked;
            DateTime? date = mn.Checked ? (DateTime?)mnDate.Value.Date : null;
            bool draft = false;
            if (date.HasValue && !MeetingNotes.Ready)
            {
                var choice = MessageBox.Show(this,
                    "Скачать локальную модель MN (1,92 ГБ)?\n\nДа — скачать модель и составить протокол.\nНет — сразу создать черновик MN из реплик без загрузки.",
                    "Как создать протокол", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (choice == DialogResult.Cancel) return;
                draft = choice == DialogResult.No;
            }
            int limit = audio && split.Checked ? (int)minutes.Value : 0;
            string speechLanguage = !TextInput && (txt || date.HasValue) ? new[] { "ru", "auto", "en" }[language.SelectedIndex] : null;
            cancel = new CancellationTokenSource();
            var token = cancel.Token;
            bar.Value = 0;
            lastResult = null;
            hasExportStatus = true;
            UpdateControls();
            try
            {
                string destination = Path.GetFullPath(folder.Text.Trim());
                var progress = new Progress<ExportProgress>(p =>
                {
                    if (!token.IsCancellationRequested && cancel != null)
                    {
                        bar.Style = p.Percent < 0 ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
                        if (p.Percent >= 0) bar.Value = p.Percent;
                        status.Text = p.Message;
                    }
                });
                lastResult = await Task.Run(() => AudioExport.Run(files, destination, limit, audio, speechLanguage, token,
                    ((IProgress<ExportProgress>)progress).Report, date, txt, draft));
                lastMn = !date.HasValue ? null : !audio && !txt ? lastResult : Path.ChangeExtension(lastResult, ".mn.txt");
                bar.Value = 100;
                status.Text = (draft ? "Сохранён черновик из реплик: " : "Сохранено: ") + Path.GetFileName(lastResult) +
                    (audio && txt ? ", расшифровка .txt" : "") + (limit > 0 ? " и папка с частями" : "") + (date.HasValue && (audio || txt) ? ", протокол MN" : "");
            }
            catch (OperationCanceledException) { bar.Value = 0; status.Text = "Сохранение отменено"; }
            catch (Exception e)
            {
                bar.Value = 0;
                status.Text = "Не удалось завершить обработку";
                if (!closePending) MessageBox.Show(this, e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                cancel.Dispose();
                cancel = null;
                bar.Style = ProgressBarStyle.Continuous;
                UpdateControls();
                if (closePending) Close();
            }
        }
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainWindow());
        }
    }
}
