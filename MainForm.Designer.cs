using System.Drawing;
using System.Windows.Forms;

namespace RNNoise_Denoiser
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        // === Controls (доступны из MainForm.cs) ===
        private Panel panelTop;
        private TableLayoutPanel tableLayout;
        private TextBox txtFfmpeg;
        private Button btnFfmpegBrowse;
        private TextBox txtModel;
        private Button btnModelBrowse;
        private TextBox txtOutput;
        private Button btnOutputBrowse;

        // Правый блок опций (вложенная таблица)
        private TableLayoutPanel rightGrid;
        private ComboBox cboAudioCodec;
        private ComboBox cboBitrate;
        private NumericUpDown numMix;
        private CheckBox chkCopyVideo;
        private CheckBox chkHighpass;
        private NumericUpDown numHighpass;
        private CheckBox chkLowpass;
        private NumericUpDown numLowpass;
        private CheckBox chkSpeechNorm;

        private Button btnAddFiles;
        private Button btnAddFolder;
        private Button btnStart;
        private Button btnCancel;

        private ListView lvQueue;
        private ColumnHeader chFile;
        private ColumnHeader chStatus;
        private ColumnHeader chProgress;
        private ColumnHeader chOutput;

        private StatusStrip statusStrip;
        private ToolStripStatusLabel tslStatus;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();

            // ---- Form ----
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1120, 720);
            Name = "MainForm";
            Text = "RNNoise Denoiser";
            StartPosition = FormStartPosition.CenterScreen;

            // ---- Top Panel ----
            panelTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 208, // было 172 — добавили запас под 4 строки
                Padding = new Padding(10),
                Name = "panelTop"
            };
            Controls.Add(panelTop);

            // ---- Основная таблица (слева поля путей, справа — вложенная таблица опций) ----
            tableLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 4,
                Name = "tableLayout"
            };
            // Колонки: 0=Label(120), 1=Text(%), 2=Browse(90), 3=RightGrid(фулл)
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 540)); // правая панель
            // Ряды
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            panelTop.Controls.Add(tableLayout);

            // ---- Row 0: FFmpeg ----
            var lblFfmpeg = new Label { Text = "FFmpeg bin:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Anchor = AnchorStyles.Left };
            tableLayout.Controls.Add(lblFfmpeg, 0, 0);

            txtFfmpeg = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, Name = "txtFfmpeg" };
            tableLayout.Controls.Add(txtFfmpeg, 1, 0);

            btnFfmpegBrowse = new Button { Text = "Обзор", Name = "btnFfmpegBrowse", Anchor = AnchorStyles.Right };
            tableLayout.Controls.Add(btnFfmpegBrowse, 2, 0);

            // ---- Row 1: Model ----
            var lblModel = new Label { Text = "RNNoise .rnnn:", AutoSize = true, Anchor = AnchorStyles.Left };
            tableLayout.Controls.Add(lblModel, 0, 1);

            txtModel = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, Name = "txtModel" };
            tableLayout.Controls.Add(txtModel, 1, 1);

            btnModelBrowse = new Button { Text = "Обзор", Name = "btnModelBrowse", Anchor = AnchorStyles.Right };
            tableLayout.Controls.Add(btnModelBrowse, 2, 1);

            // ---- Row 2: Output ----
            var lblOutput = new Label { Text = "Папка вывода:", AutoSize = true, Anchor = AnchorStyles.Left };
            tableLayout.Controls.Add(lblOutput, 0, 2);

            txtOutput = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, Name = "txtOutput" };
            tableLayout.Controls.Add(txtOutput, 1, 2);

            btnOutputBrowse = new Button { Text = "Обзор", Name = "btnOutputBrowse", Anchor = AnchorStyles.Right };
            tableLayout.Controls.Add(btnOutputBrowse, 2, 2);

            // ---- Вложенная таблица справа (опции) ----
            rightGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 4,
                Name = "rightGrid",
                Margin = new Padding(10, 0, 0, 0)
            };
            // Правые колонки: Label(120), Control(160), Label(120), Control(140)
            rightGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            rightGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            rightGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            rightGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            rightGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            rightGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            rightGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            rightGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            // row0: Кодек/Битрейт
            var lblCodec = new Label { Text = "Кодек аудио:", AutoSize = true, Anchor = AnchorStyles.Left };
            rightGrid.Controls.Add(lblCodec, 0, 0);
            cboAudioCodec = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Name = "cboAudioCodec", Anchor = AnchorStyles.Left | AnchorStyles.Right };
            cboAudioCodec.Items.AddRange(new object[] { "aac", "libmp3lame", "pcm_s16le" });
            cboAudioCodec.SelectedIndex = 0;
            rightGrid.Controls.Add(cboAudioCodec, 1, 0);

            var lblBr = new Label { Text = "Битрейт:", AutoSize = true, Anchor = AnchorStyles.Left };
            rightGrid.Controls.Add(lblBr, 2, 0);
            cboBitrate = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Name = "cboBitrate", Anchor = AnchorStyles.Left | AnchorStyles.Right };
            cboBitrate.Items.AddRange(new object[] { "128k", "160k", "192k", "256k", "320k" });
            cboBitrate.SelectedIndex = 2;
            rightGrid.Controls.Add(cboBitrate, 3, 0);

            // row1: mix / Highpass
            var lblMix = new Label { Text = "mix (0–1):", AutoSize = true, Anchor = AnchorStyles.Left };
            rightGrid.Controls.Add(lblMix, 0, 1);
            numMix = new NumericUpDown
            {
                DecimalPlaces = 2,
                Increment = 0.05M,
                Minimum = 0M,
                Maximum = 1M,
                Value = 0.85M,
                Name = "numMix",
                Anchor = AnchorStyles.Left
            };
            rightGrid.Controls.Add(numMix, 1, 1);

            var lblHp = new Label { Text = "Highpass (Гц):", AutoSize = true, Anchor = AnchorStyles.Left };
            rightGrid.Controls.Add(lblHp, 2, 1);
            var hpPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            chkHighpass = new CheckBox { Text = "Вкл", Name = "chkHighpass", AutoSize = true };
            numHighpass = new NumericUpDown { Minimum = 20, Maximum = 300, Value = 80, Enabled = false, Name = "numHighpass", Width = 80 };
            hpPanel.Controls.Add(chkHighpass);
            hpPanel.Controls.Add(numHighpass);
            rightGrid.Controls.Add(hpPanel, 3, 1);

            // row2: SpeechNorm / Lowpass
            var lblSn = new Label { Text = "SpeechNorm:", AutoSize = true, Anchor = AnchorStyles.Left };
            rightGrid.Controls.Add(lblSn, 0, 2);
            chkSpeechNorm = new CheckBox { Text = "Вкл", Name = "chkSpeechNorm", AutoSize = true, Anchor = AnchorStyles.Left };
            rightGrid.Controls.Add(chkSpeechNorm, 1, 2);

            var lblLp = new Label { Text = "Lowpass (Гц):", AutoSize = true, Anchor = AnchorStyles.Left };
            rightGrid.Controls.Add(lblLp, 2, 2);
            var lpPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            chkLowpass = new CheckBox { Text = "Вкл", Name = "chkLowpass", AutoSize = true };
            numLowpass = new NumericUpDown { Minimum = 4000, Maximum = 20000, Value = 12000, Enabled = false, Name = "numLowpass", Width = 80 };
            lpPanel.Controls.Add(chkLowpass);
            lpPanel.Controls.Add(numLowpass);
            rightGrid.Controls.Add(lpPanel, 3, 2);

            // row3: Copy Video
            var lblCopy = new Label { Text = "Копировать видео:", AutoSize = true, Anchor = AnchorStyles.Left };
            rightGrid.Controls.Add(lblCopy, 0, 3);
            chkCopyVideo = new CheckBox { Text = "Вкл", Name = "chkCopyVideo", Checked = true, AutoSize = true, Anchor = AnchorStyles.Left };
            rightGrid.Controls.Add(chkCopyVideo, 1, 3);

            // Помещаем rightGrid в правую колонку и растягиваем по первым четырем строкам
            tableLayout.Controls.Add(rightGrid, 3, 0);
            tableLayout.SetRowSpan(rightGrid, 4);

            // ---- Row 3: кнопки слева ----
            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 6, 0, 0) };
            btnAddFiles = new Button { Text = "Добавить файлы", Name = "btnAddFiles" };
            btnAddFolder = new Button { Text = "Добавить папку", Name = "btnAddFolder" };
            btnStart = new Button { Text = "Старт", Name = "btnStart" };
            btnCancel = new Button { Text = "Отмена", Name = "btnCancel", Enabled = false };
            btnPanel.Controls.Add(btnAddFiles);
            btnPanel.Controls.Add(btnAddFolder);
            btnPanel.Controls.Add(btnStart);
            btnPanel.Controls.Add(btnCancel);
            tableLayout.Controls.Add(btnPanel, 1, 3);

            // ---- Queue ----
            lvQueue = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                Name = "lvQueue"
            };
            chFile = new ColumnHeader { Text = "Файл", Width = 520 };
            chStatus = new ColumnHeader { Text = "Статус", Width = 120 };
            chProgress = new ColumnHeader { Text = "Прогресс", Width = 100 };
            chOutput = new ColumnHeader { Text = "Выход", Width = 320 };
            lvQueue.Columns.AddRange(new ColumnHeader[] { chFile, chStatus, chProgress, chOutput });
            Controls.Add(lvQueue);
            lvQueue.BringToFront();

            // ---- Status ----
            statusStrip = new StatusStrip { Name = "statusStrip" };
            tslStatus = new ToolStripStatusLabel("Готов") { Name = "tslStatus" };
            statusStrip.Items.Add(tslStatus);
            Controls.Add(statusStrip);
        }
        #endregion
    }
}
