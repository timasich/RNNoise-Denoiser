using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;

namespace RNNoise_Denoiser
{
    public partial class MainForm : Form
    {
        CancellationTokenSource? _cts;
        AppSettings _settings = null!;
        readonly string _settingsPath;
        readonly ToolTip _tip;

        static readonly string[] VideoExts = { ".mp4", ".mov", ".mkv", ".m4v", ".avi", ".webm" };
        static readonly string[] AudioExts = { ".wav", ".mp3", ".m4a", ".aac", ".flac", ".ogg" };
        static readonly Dictionary<string, DenoiseProfile> DefaultPresets = new()
        {
            ["Soft"] = new DenoiseProfile { Mix = 0.9 },
            ["Std"] = new DenoiseProfile { Mix = 0.85 },
            ["Aggressive"] = new DenoiseProfile { Mix = 0.7, LowpassHz = 12000, HighpassHz = 80 }
        };

        static string ModelPathForFfmpeg(string path) => path.Replace("\\", "/").Replace(":", "\\:");

        public MainForm()
        {
            InitializeComponent();
            _tip = new ToolTip();

            foreach (var l in Localizer.Langs)
                cboLang.Items.Add(l);

            // minor toggles
            chkHighpass.CheckedChanged += (s, e) => numHighpass.Enabled = chkHighpass.Checked;
            chkLowpass.CheckedChanged += (s, e) => numLowpass.Enabled = chkLowpass.Checked;
            // Settings path
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "RNNoiseDenoiser");
            Directory.CreateDirectory(dir);
            _settingsPath = Path.Combine(dir, "settings.json");
            _settings = AppSettings.Load(_settingsPath);

            if (string.IsNullOrWhiteSpace(_settings.Language))
            {
                _settings.Language = "en";
                _settings.Save(_settingsPath);
            }

            var langItem = Localizer.Langs.FirstOrDefault(l => l.Code == _settings.Language) ?? Localizer.Langs[0];
            Localizer.Set(langItem.Code);
            cboLang.SelectedItem = langItem;

            // Defaults to your paths (editable in UI)
            if (string.IsNullOrWhiteSpace(_settings.FfmpegBinPath))
                _settings.FfmpegBinPath = @"C:\Tools\ffmpeg\bin";
            if (string.IsNullOrWhiteSpace(_settings.ModelPath))
                _settings.ModelPath = @"C:\Tools\rnnoise\models\sh.rnnn";
            if (string.IsNullOrWhiteSpace(_settings.OutputFolder))
                _settings.OutputFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            LoadSettingsToUi();
            WireEvents();
            ApplyLocalization();
            LoadPresetsToUi();
            SelectPresetFromProfile(_settings.Profile);
            ApplyTheme();
            ShowReadmeIfNeeded();

            AllowDrop = true;
            DragEnter += (s, e) => { if (e.Data!.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragDrop += (s, e) => EnqueueFiles((string[])e.Data!.GetData(DataFormats.FileDrop)!);
        }

        void WireEvents()
        {
            btnFfmpegBrowse.Click += (s, e) =>
            {
                using var fbd = new FolderBrowserDialog { Description = Localizer.Tr("Folder with ffmpeg/bin (ffmpeg.exe, ffprobe.exe)") };
                if (fbd.ShowDialog(this) == DialogResult.OK) txtFfmpeg.Text = fbd.SelectedPath;
            };
            btnModelBrowse.Click += (s, e) =>
            {
                string filter = $"{Localizer.Tr("RNNoise model file")} (*.rnnn)|*.rnnn|{Localizer.Tr("All files")}|*.*";
                using var ofd = new OpenFileDialog { Filter = filter };
                if (ofd.ShowDialog(this) == DialogResult.OK) txtModel.Text = ofd.FileName;
            };
            btnOutputBrowse.Click += (s, e) =>
            {
                using var fbd = new FolderBrowserDialog { Description = Localizer.Tr("Output folder") };
                if (fbd.ShowDialog(this) == DialogResult.OK) txtOutput.Text = fbd.SelectedPath;
            };
            btnAddFiles.Click += (s, e) =>
            {
                string filter = $"{Localizer.Tr("Video/Audio")}|*.mp4;*.mov;*.mkv;*.m4v;*.avi;*.webm;*.wav;*.mp3;*.m4a;*.aac;*.flac;*.ogg|{Localizer.Tr("All files")}|*.*";
                using var ofd = new OpenFileDialog { Filter = filter, Multiselect = true };
                if (ofd.ShowDialog(this) == DialogResult.OK) EnqueueFiles(ofd.FileNames);
            };
            btnAddFolder.Click += (s, e) =>
            {
                using var fbd = new FolderBrowserDialog { Description = Localizer.Tr("Folder with files") };
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    var files = Directory.EnumerateFiles(fbd.SelectedPath, "*.*", SearchOption.AllDirectories)
                        .Where(p => VideoExts.Concat(AudioExts).Contains(Path.GetExtension(p).ToLowerInvariant()))
                        .ToArray();
                    EnqueueFiles(files);
                }
            };
            btnCheckEnv.Click += (s, e) => CheckEnvironment();
            btnPreview.Click += async (s, e) => await PreviewAsync();
            btnStart.Click += async (s, e) => await StartAsync();
            btnCancel.Click += (s, e) => _cts?.Cancel();
            FormClosing += (s, e) => SaveSettingsFromUi();

            cboLang.SelectedIndexChanged += (s, e) =>
            {
                if (cboLang.SelectedItem is LangItem li)
                {
                    Localizer.Set(li.Code);
                    ApplyLocalization();
                    _settings.Language = li.Code;
                    _settings.Save(_settingsPath);
                }
            };

            lvQueue.DoubleClick += (s, e) =>
            {
                if (lvQueue.SelectedItems.Count == 0) return;
                var qi = (QueueItem)lvQueue.SelectedItems[0].Tag!;
                if (string.IsNullOrWhiteSpace(qi.Output)) return;
                try
                {
                    if (File.Exists(qi.Output))
                        Process.Start("explorer.exe", $"/select,\"{qi.Output}\"");
                    else
                    {
                        var dir = Path.GetDirectoryName(qi.Output);
                        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                            Process.Start("explorer.exe", dir);
                    }
                }
                catch { }
            };
            cboPreset.SelectedIndexChanged += (s, e) =>
            {
                if (cboPreset.SelectedItem is string name)
                    ApplyPreset(name);
            };
            btnSavePreset.Click += (s, e) => SavePreset();
            btnRenamePreset.Click += (s, e) => RenamePreset();
            btnDeletePreset.Click += (s, e) => DeletePreset();
            tslMadeBy.DoubleClick += (s, e) => ShowReadme();
        }

        void ApplyLocalization()
        {
            lblFfmpeg.Text = Localizer.Tr("FFmpeg bin:");
            btnFfmpegBrowse.Text = Localizer.Tr("Browse");
            lblModel.Text = Localizer.Tr("RNNoise .rnnn:");
            btnModelBrowse.Text = Localizer.Tr("Browse");
            lblOutput.Text = Localizer.Tr("Output folder:");
            btnOutputBrowse.Text = Localizer.Tr("Browse");
            lblCodec.Text = Localizer.Tr("Audio codec:");
            lblBr.Text = Localizer.Tr("Bitrate:");
            lblMix.Text = Localizer.Tr("mix (0-1):");
            lblHp.Text = Localizer.Tr("Highpass (Hz):");
            chkHighpass.Text = Localizer.Tr("On");
            lblSn.Text = Localizer.Tr("SpeechNorm:");
            chkSpeechNorm.Text = Localizer.Tr("On");
            lblLp.Text = Localizer.Tr("Lowpass (Hz):");
            chkLowpass.Text = Localizer.Tr("On");
            lblCopy.Text = Localizer.Tr("Copy video:");
            chkCopyVideo.Text = Localizer.Tr("On");
            lblPreset.Text = Localizer.Tr("Preset:");
            btnSavePreset.Text = Localizer.Tr("Save");
            btnRenamePreset.Text = Localizer.Tr("Rename");
            btnDeletePreset.Text = Localizer.Tr("Delete");
            btnAddFiles.Text = Localizer.Tr("Add files");
            btnAddFolder.Text = Localizer.Tr("Add folder");
            btnCheckEnv.Text = Localizer.Tr("Check env");
            btnPreview.Text = Localizer.Tr("Preview");
            btnStart.Text = Localizer.Tr("Start");
            btnCancel.Text = Localizer.Tr("Cancel");
            chFile.Text = Localizer.Tr("File");
            chStatus.Text = Localizer.Tr("Status");
            chProgress.Text = Localizer.Tr("Progress");
            chTime.Text = Localizer.Tr("Time");
            chOutput.Text = Localizer.Tr("Output");
            tslStatus.Text = Localizer.Tr("Ready");

            _tip.SetToolTip(lblFfmpeg, Localizer.Tr("Folder with ffmpeg.exe and ffprobe.exe"));
            _tip.SetToolTip(txtFfmpeg, Localizer.Tr("Folder with ffmpeg.exe and ffprobe.exe"));
            _tip.SetToolTip(btnFfmpegBrowse, Localizer.Tr("Select ffmpeg/bin folder"));
            _tip.SetToolTip(lblModel, Localizer.Tr("RNNoise model file (.rnnn)"));
            _tip.SetToolTip(txtModel, Localizer.Tr("RNNoise model file (.rnnn)"));
            _tip.SetToolTip(btnModelBrowse, Localizer.Tr("Select RNNoise model file"));
            _tip.SetToolTip(lblOutput, Localizer.Tr("Folder to save processed files"));
            _tip.SetToolTip(txtOutput, Localizer.Tr("Folder to save processed files"));
            _tip.SetToolTip(btnOutputBrowse, Localizer.Tr("Select output folder"));
            _tip.SetToolTip(lblCodec, Localizer.Tr("Audio codec of output file"));
            _tip.SetToolTip(cboAudioCodec, Localizer.Tr("Audio codec of output file"));
            _tip.SetToolTip(lblBr, Localizer.Tr("Audio bitrate: higher = better quality but larger file"));
            _tip.SetToolTip(cboBitrate, Localizer.Tr("Audio bitrate: higher = better quality but larger file"));
            _tip.SetToolTip(lblMix, Localizer.Tr("Noise reduction amount: 0 = no processing, 1 = maximum reduction"));
            _tip.SetToolTip(numMix, Localizer.Tr("Noise reduction amount: 0 = no processing, 1 = maximum reduction"));
            _tip.SetToolTip(lblHp, Localizer.Tr("Removes frequencies below specified value, helps remove hum"));
            _tip.SetToolTip(chkHighpass, Localizer.Tr("Removes frequencies below specified value, helps remove hum"));
            _tip.SetToolTip(numHighpass, Localizer.Tr("High-pass filter cutoff frequency (Hz)"));
            _tip.SetToolTip(lblSn, Localizer.Tr("Normalizes speech loudness"));
            _tip.SetToolTip(chkSpeechNorm, Localizer.Tr("Normalizes speech loudness"));
            _tip.SetToolTip(lblLp, Localizer.Tr("Removes frequencies above specified value, suppresses HF noise"));
            _tip.SetToolTip(chkLowpass, Localizer.Tr("Removes frequencies above specified value, suppresses HF noise"));
            _tip.SetToolTip(numLowpass, Localizer.Tr("Low-pass filter cutoff frequency (Hz)"));
            _tip.SetToolTip(lblCopy, Localizer.Tr("Do not re-encode video, replace audio only"));
            _tip.SetToolTip(chkCopyVideo, Localizer.Tr("Do not re-encode video, replace audio only"));
            _tip.SetToolTip(btnAddFiles, Localizer.Tr("Add files to queue"));
            _tip.SetToolTip(btnAddFolder, Localizer.Tr("Add all supported files from folder"));
            _tip.SetToolTip(btnCheckEnv, Localizer.Tr("Check required tools"));
            _tip.SetToolTip(btnPreview, Localizer.Tr("Preview selected file"));
            _tip.SetToolTip(btnStart, Localizer.Tr("Start processing selected files"));
            _tip.SetToolTip(btnCancel, Localizer.Tr("Cancel current processing"));

            BuildContextMenu();
        }

        void ApplyTheme()
        {
            Theme.Apply(this);
            panelTop.BackColor = Theme.BgSurface;
            tableLayout.BackColor = Theme.BgSurface;
            rightGrid.BackColor = Theme.BgElevated;

            foreach (var lbl in new[] { lblFfmpeg, lblModel, lblOutput, lblCodec, lblBr, lblMix, lblHp, lblSn, lblLp, lblCopy, lblPreset })
                lbl.ForeColor = Theme.TextSecondary;

            foreach (Control ctrl in new Control[] { txtFfmpeg, txtModel, txtOutput, numMix, numHighpass, numLowpass })
                Theme.StyleInput(ctrl);

            foreach (var box in new[] { cboAudioCodec, cboBitrate, cboPreset, cboLang.ComboBox })
                Theme.StyleComboBox(box);

            foreach (var chk in new[] { chkCopyVideo, chkHighpass, chkLowpass, chkSpeechNorm })
            {
                chk.ForeColor = Theme.TextPrimary;
                chk.BackColor = Theme.BgSurface;
            }

            Theme.StylePrimary(btnStart);
            Theme.StylePrimary(btnSavePreset);
            Theme.StyleSecondary(btnFfmpegBrowse);
            Theme.StyleSecondary(btnModelBrowse);
            Theme.StyleSecondary(btnOutputBrowse);
            Theme.StyleSecondary(btnAddFiles);
            Theme.StyleSecondary(btnAddFolder);
            Theme.StyleSecondary(btnCheckEnv);
            Theme.StyleSecondary(btnPreview);
            Theme.StyleSecondary(btnCancel);
            Theme.StyleSecondary(btnRenamePreset);
            Theme.StyleDanger(btnDeletePreset);

            lvQueue.BackColor = ColorTranslator.FromHtml("#0E1628");
            lvQueue.ForeColor = Theme.TextPrimary;
            lvQueue.BorderStyle = BorderStyle.FixedSingle;
            lvQueue.OwnerDraw = true;
            lvQueue.DrawColumnHeader += LvQueue_DrawColumnHeader;
            lvQueue.DrawItem += LvQueue_DrawItem;
            lvQueue.DrawSubItem += LvQueue_DrawSubItem;
            Theme.UseDarkScrollbars(lvQueue);

            Theme.StyleStatusStrip(statusStrip);
            tslStatus.ForeColor = Theme.TextSecondary;
            tslMadeBy.ForeColor = Theme.TextSecondary;
            cboLang.ComboBox.BackColor = Theme.BgElevated;
            cboLang.ComboBox.ForeColor = Theme.TextPrimary;
        }

        void LvQueue_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
        {
            using var back = new SolidBrush(Theme.BgElevated);
            using var border = new Pen(Theme.LineBorder);
            using var fore = new SolidBrush(Theme.TextSecondary);
            e.Graphics.FillRectangle(back, e.Bounds);
            e.Graphics.DrawRectangle(border, e.Bounds);
            var rect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 4, e.Bounds.Height);
            e.Graphics.DrawString(e.Header.Text, e.Font, fore, rect, new StringFormat { LineAlignment = StringAlignment.Center });
        }

        void LvQueue_DrawItem(object? sender, DrawListViewItemEventArgs e) => e.DrawDefault = true;

        void LvQueue_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
        {
            e.DrawBackground();
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, lvQueue.Font, e.Bounds,
                Theme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        void BuildContextMenu()
        {
            var ctx = new ContextMenuStrip();
            ctx.Items.Add(Localizer.Tr("Remove from queue"), null, (s, e) =>
            {
                foreach (ListViewItem it in lvQueue.SelectedItems.Cast<ListViewItem>().ToArray())
                    lvQueue.Items.Remove(it);
            });
            ctx.Items.Add(Localizer.Tr("Duplicate"), null, (s, e) =>
            {
                foreach (ListViewItem it in lvQueue.SelectedItems.Cast<ListViewItem>().ToArray())
                {
                    var qi = (QueueItem)it.Tag!;
                    var copy = new ListViewItem(new[] { "", qi.Input, Localizer.Tr("Queued"), "0%", "", "" })
                    { Tag = new QueueItem { Input = qi.Input }, Checked = true };
                    lvQueue.Items.Add(copy);
                }
            });
            ctx.Items.Add(Localizer.Tr("Open folder with original"), null, (s, e) =>
            {
                if (lvQueue.SelectedItems.Count == 0) return;
                var qi = (QueueItem)lvQueue.SelectedItems[0].Tag!;
                try { Process.Start("explorer.exe", $"/select,\"{qi.Input}\""); } catch { }
            });
            ctx.Items.Add(Localizer.Tr("Open output folder"), null, (s, e) =>
            {
                if (lvQueue.SelectedItems.Count == 0) return;
                var qi = (QueueItem)lvQueue.SelectedItems[0].Tag!;
                if (string.IsNullOrWhiteSpace(qi.Output)) return;
                try
                {
                    if (File.Exists(qi.Output))
                        Process.Start("explorer.exe", $"/select,\"{qi.Output}\"");
                    else
                    {
                        var dir = Path.GetDirectoryName(qi.Output);
                        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                            Process.Start("explorer.exe", dir);
                    }
                }
                catch { }
            });
            ctx.Items.Add(new ToolStripSeparator());
            ctx.Items.Add(Localizer.Tr("Mark for cleanup"), null, (s, e) =>
            {
                foreach (ListViewItem it in lvQueue.SelectedItems.Cast<ListViewItem>())
                    it.Checked = true;
            });
            ctx.Items.Add(Localizer.Tr("Unmark from cleanup"), null, (s, e) =>
            {
                foreach (ListViewItem it in lvQueue.SelectedItems.Cast<ListViewItem>())
                    it.Checked = false;
            });
            lvQueue.ContextMenuStrip = ctx;
        }

        void LoadSettingsToUi()
        {
            txtFfmpeg.Text = _settings.FfmpegBinPath;
            txtModel.Text = _settings.ModelPath;
            txtOutput.Text = _settings.OutputFolder;

            numMix.Value = (decimal)_settings.Profile.Mix;
            chkHighpass.Checked = _settings.Profile.HighpassHz.HasValue;
            if (_settings.Profile.HighpassHz.HasValue) numHighpass.Value = _settings.Profile.HighpassHz.Value;
            chkLowpass.Checked = _settings.Profile.LowpassHz.HasValue;
            if (_settings.Profile.LowpassHz.HasValue) numLowpass.Value = _settings.Profile.LowpassHz.Value;
            chkSpeechNorm.Checked = _settings.Profile.SpeechNorm;

            cboAudioCodec.SelectedItem = _settings.AudioCodec;
            cboBitrate.SelectedItem = _settings.AudioBitrate;
            chkCopyVideo.Checked = _settings.CopyVideo;
            var lang = Localizer.Langs.FirstOrDefault(l => l.Code == _settings.Language) ?? Localizer.Langs[0];
            cboLang.SelectedItem = lang;
        }

        void SaveSettingsFromUi()
        {
            _settings.FfmpegBinPath = txtFfmpeg.Text.Trim();
            _settings.ModelPath = txtModel.Text.Trim();
            _settings.OutputFolder = txtOutput.Text.Trim();
            _settings.Profile = new DenoiseProfile
            {
                Mix = (double)numMix.Value,
                HighpassHz = chkHighpass.Checked ? (int?)numHighpass.Value : null,
                LowpassHz = chkLowpass.Checked ? (int?)numLowpass.Value : null,
                SpeechNorm = chkSpeechNorm.Checked
            };
            _settings.AudioCodec = cboAudioCodec.SelectedItem?.ToString() ?? "aac";
            _settings.AudioBitrate = cboBitrate.SelectedItem?.ToString() ?? "192k";
            _settings.CopyVideo = chkCopyVideo.Checked;
            if (cboLang.SelectedItem is LangItem li) _settings.Language = li.Code; else _settings.Language = "en";
            _settings.Save(_settingsPath);
        }

        void EnqueueFiles(IEnumerable<string> files)
        {
            int added = 0;
            foreach (var f in files.Distinct())
            {
                var it = new ListViewItem(new[] { "", f, Localizer.Tr("Queued"), "0%", "", "" })
                { Tag = new QueueItem { Input = f }, Checked = true };
                lvQueue.Items.Add(it);
                added++;
            }
            tslStatus.Text = string.Format(Localizer.Tr("Added files: {0}"), added);
        }

        async Task StartAsync()
        {
            SaveSettingsFromUi();
            if (!ValidatePaths()) return;

            btnStart.Enabled = false;
            btnCancel.Enabled = true;
            _cts = new CancellationTokenSource();
            tslStatus.Text = Localizer.Tr("Processing...");

            try
            {
                foreach (ListViewItem it in lvQueue.Items)
                {
                    if (_cts.IsCancellationRequested) break;
                    if (!it.Checked) continue;

                    var qi = (QueueItem)it.Tag!;
                    it.SubItems[2].Text = Localizer.Tr("Preparing");
                    it.SubItems[3].Text = "0%";
                    it.SubItems[4].Text = "";

                    var output = BuildOutputPath(qi.Input);
                    qi.Output = output;
                    it.SubItems[5].Text = output;

                    double? duration = await ProbeDurationAsync(qi.Input);
                    bool hasVideo = await ProbeHasVideoAsync(qi.Input);

                    it.SubItems[2].Text = "FFmpeg";
                    var sw = Stopwatch.StartNew();
                    var (ok, err) = await RunFfmpegAsync(qi.Input, output, hasVideo, duration,
                        p => BeginInvoke(new Action(() => it.SubItems[3].Text = p)),
                        rem => BeginInvoke(new Action(() =>
                        {
                            it.SubItems[4].Text = rem.ToString("hh\\:mm\\:ss");
                            tslStatus.Text = string.Format(Localizer.Tr("Remaining {0}"), rem.ToString("hh\\:mm\\:ss"));
                        })),
                        _cts.Token);
                    sw.Stop();

                    BeginInvoke(new Action(() =>
                    {
                        it.SubItems[2].Text = ok ? Localizer.Tr("Done") : (Localizer.Tr("Error:") + " " + err);
                        if (ok) it.SubItems[3].Text = "100%";
                        it.SubItems[4].Text = sw.Elapsed.ToString("hh\\:mm\\:ss");
                        if (ok) it.Checked = false;
                        tslStatus.Text = ok ? string.Format(Localizer.Tr("Time: {0}"), sw.Elapsed.ToString("hh\\:mm\\:ss")) : Localizer.Tr("Ready");
                    }));
                }
            }
            finally
            {
                btnStart.Enabled = true;
                btnCancel.Enabled = false;
                _cts = null;
                tslStatus.Text = Localizer.Tr("Ready");
            }
        }

        bool ValidatePaths()
        {
            string ffmpeg = Path.Combine(txtFfmpeg.Text.Trim(), "ffmpeg.exe");
            string ffprobe = Path.Combine(txtFfmpeg.Text.Trim(), "ffprobe.exe");
            if (!File.Exists(ffmpeg) || !File.Exists(ffprobe))
            {
                MessageBox.Show(this, Localizer.Tr("ffmpeg.exe/ffprobe.exe not found. Specify path to bin folder."), Localizer.Tr("Error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            if (!File.Exists(txtModel.Text.Trim()))
            {
                MessageBox.Show(this, Localizer.Tr(".rnnn model file not found."), Localizer.Tr("Error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            if (string.IsNullOrWhiteSpace(txtOutput.Text) || !Directory.Exists(txtOutput.Text))
            {
                MessageBox.Show(this, Localizer.Tr("Specify an existing output folder."), Localizer.Tr("Error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }

        string BuildOutputPath(string input)
        {
            var outDir = txtOutput.Text.Trim();
            var name = Path.GetFileNameWithoutExtension(input) + "_clean" + Path.GetExtension(input);
            return Path.Combine(outDir, name);
        }

        async Task<double?> ProbeDurationAsync(string input)
        {
            try
            {
                var ffprobe = Path.Combine(txtFfmpeg.Text.Trim(), "ffprobe.exe");
                var psi = new ProcessStartInfo
                {
                    FileName = ffprobe,
                    Arguments = $"-v error -show_entries format=duration -of default=nokey=1:noprint_wrappers=1 \"{input}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                using var p = Process.Start(psi)!;
                string txt = await p.StandardOutput.ReadToEndAsync();
                await p.WaitForExitAsync();
                if (double.TryParse(txt.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var dur))
                    return dur;
            }
            catch { }
            return null;
        }

        async Task<bool> ProbeHasVideoAsync(string input)
        {
            try
            {
                var ffprobe = Path.Combine(txtFfmpeg.Text.Trim(), "ffprobe.exe");
                var psi = new ProcessStartInfo
                {
                    FileName = ffprobe,
                    Arguments = $"-v error -select_streams v:0 -show_entries stream=codec_type -of csv=p=0 \"{input}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                using var p = Process.Start(psi)!;
                string txt = await p.StandardOutput.ReadToEndAsync();
                await p.WaitForExitAsync();
                return txt.Trim().Length > 0;
            }
            catch { return false; }
        }

        (string audioCodec, string audioBitrate) ResolveAudioCodec(string inputExt)
        {
            string codec = (cboAudioCodec.SelectedItem?.ToString() ?? "aac").ToLowerInvariant();
            string br = (cboBitrate.SelectedItem?.ToString() ?? "192k").ToLowerInvariant();

            if (inputExt.Equals(".wav", StringComparison.OrdinalIgnoreCase))
            {
                codec = "pcm_s16le";
                br = string.Empty;
            }
            return (codec, br);
        }

        async Task<(bool ok, string err)> RunFfmpegAsync(
            string input, string output, bool hasVideo, double? duration,
            Action<string> reportProgress, Action<TimeSpan> reportRemaining, CancellationToken ct)
        {
            string ffmpeg = Path.Combine(txtFfmpeg.Text.Trim(), "ffmpeg.exe");

            string filterChain = BuildFilterChain();

            var ext = Path.GetExtension(input).ToLowerInvariant();
            var (aCodec, aBr) = ResolveAudioCodec(ext);

            var args = new StringBuilder();
            args.Append($"-y -hide_banner -i \"{input}\" ");
            args.Append("-map 0 ");
            if (hasVideo && chkCopyVideo.Checked) args.Append("-c:v copy ");
            //args.Append($"-af \"{filterChain}\" ");
            args.Append($"-af {filterChain} ");
            args.Append($"-c:a {aCodec} ");
            if (!string.IsNullOrEmpty(aBr) && aCodec != "pcm_s16le") args.Append($"-b:a {aBr} ");
            if (ext == ".mp4" || ext == ".m4v") args.Append("-movflags +faststart ");
            args.Append("-shortest ");
            args.Append($"\"{output}\"");

            var psi = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = args.ToString(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                StandardErrorEncoding = Encoding.UTF8,
                StandardOutputEncoding = UTF8Encoding.UTF8
            };

            try
            {
                using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
                p.Start();
                using var reg = ct.Register(() => { try { if (!p.HasExited) p.Kill(true); } catch { } });

                var timeRe = new Regex(@"time=(\d+):(\d+):(\d+\.?\d*)", RegexOptions.Compiled);
                var stderrSb = new StringBuilder();
                var stderrTask = Task.Run(async () =>
                {
                    string? line;
                    while ((line = await p.StandardError.ReadLineAsync()) != null && !ct.IsCancellationRequested)
                    {
                        stderrSb.AppendLine(line);
                        var m = timeRe.Match(line);
                        if (m.Success && duration.HasValue)
                        {
                            double h = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                            double mi = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                            double s = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                            double sec = h * 3600 + mi * 60 + s;
                            double pct = Math.Max(0, Math.Min(100, (sec / duration.Value) * 100.0));
                            reportProgress($"{pct:0}%");
                            var remain = duration.Value - sec;
                            if (remain >= 0) reportRemaining(TimeSpan.FromSeconds(remain));
                        }
                    }
                });

                await Task.WhenAll(p.WaitForExitAsync(), stderrTask);
                if (ct.IsCancellationRequested) return (false, Localizer.Tr("Cancelled"));
                if (p.ExitCode == 0) return (true, "");
                string errTxt = stderrSb.ToString().Trim();
                if (errTxt.Length > 1000) errTxt = errTxt[^1000..];
                return (false, $"FFmpeg code {p.ExitCode}: {errTxt}");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }

        }

        static string EscapeForFilter(string path) => path.Replace("\\", "\\\\");

        string BuildFilterChain()
        {
            var filters = new List<string>();
            if (chkHighpass.Checked) filters.Add($"highpass=f={(int)numHighpass.Value}");
            if (chkLowpass.Checked) filters.Add($"lowpass=f={(int)numLowpass.Value}");
            filters.Add("aresample=48000");
            filters.Add($"arnndn=m='{ModelPathForFfmpeg(txtModel.Text.Trim())}':mix={((double)numMix.Value).ToString(CultureInfo.InvariantCulture)}");
            if (chkSpeechNorm.Checked) filters.Add("speechnorm=e=6");
            return string.Join(',', filters);
        }

        async Task PreviewAsync()
        {
            if (lvQueue.SelectedItems.Count == 0)
            {
                MessageBox.Show(this, Localizer.Tr("Select a file in queue"), Localizer.Tr("Preview"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string ffplay = Path.Combine(txtFfmpeg.Text.Trim(), "ffplay.exe");
            if (!File.Exists(ffplay))
            {
                MessageBox.Show(this, "ffplay.exe not found", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!File.Exists(txtModel.Text.Trim()))
            {
                MessageBox.Show(this, Localizer.Tr(".rnnn model file not found."), Localizer.Tr("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var qi = (QueueItem)lvQueue.SelectedItems[0].Tag!;
            string filterChain = BuildFilterChain();
            var psi = new ProcessStartInfo
            {
                FileName = ffplay,
                Arguments = $"-autoexit -hide_banner -i \"{qi.Input}\" -af {filterChain}",
                UseShellExecute = false,
                CreateNoWindow = false,
            };
            try { Process.Start(psi); } catch { }
            await Task.CompletedTask;
        }

        void CheckEnvironment()
        {
            string ffmpeg = Path.Combine(txtFfmpeg.Text.Trim(), "ffmpeg.exe");
            string ffprobe = Path.Combine(txtFfmpeg.Text.Trim(), "ffprobe.exe");
            bool ffmpegOk = File.Exists(ffmpeg);
            bool ffprobeOk = File.Exists(ffprobe);
            bool arnndnOk = false;
            if (ffmpegOk)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = ffmpeg,
                        Arguments = "-hide_banner -filters",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };
                    using var p = Process.Start(psi)!;
                    string txt = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    arnndnOk = txt.Contains("arnndn");
                }
                catch { }
            }
            bool modelOk = File.Exists(txtModel.Text.Trim());
            var sb = new StringBuilder();
            sb.AppendLine($"ffmpeg: {(ffmpegOk ? Localizer.Tr("OK") : Localizer.Tr("Missing"))}");
            sb.AppendLine($"ffprobe: {(ffprobeOk ? Localizer.Tr("OK") : Localizer.Tr("Missing"))}");
            sb.AppendLine($"{Localizer.Tr("arnndn filter")}: {(arnndnOk ? Localizer.Tr("OK") : Localizer.Tr("Missing"))}");
            sb.AppendLine($"{Localizer.Tr("model")}: {(modelOk ? Localizer.Tr("OK") : Localizer.Tr("Missing"))}");
            MessageBox.Show(this, sb.ToString(), Localizer.Tr("Environment"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void LoadPresetsToUi()
        {
            cboPreset.Items.Clear();
            foreach (var kv in DefaultPresets)
                cboPreset.Items.Add(kv.Key);
            foreach (var kv in _settings.CustomPresets)
                cboPreset.Items.Add(kv.Key);
        }

        void ApplyPreset(string name)
        {
            if (DefaultPresets.TryGetValue(name, out var prof) || _settings.CustomPresets.TryGetValue(name, out prof))
                ApplyProfile(prof);
        }

        void ApplyProfile(DenoiseProfile p)
        {
            numMix.Value = (decimal)p.Mix;
            if (p.HighpassHz.HasValue)
            {
                chkHighpass.Checked = true;
                numHighpass.Value = p.HighpassHz.Value;
            }
            else chkHighpass.Checked = false;
            if (p.LowpassHz.HasValue)
            {
                chkLowpass.Checked = true;
                numLowpass.Value = p.LowpassHz.Value;
            }
            else chkLowpass.Checked = false;
            chkSpeechNorm.Checked = p.SpeechNorm;
        }

        DenoiseProfile BuildProfileFromUi() => new()
        {
            Mix = (double)numMix.Value,
            HighpassHz = chkHighpass.Checked ? (int?)numHighpass.Value : null,
            LowpassHz = chkLowpass.Checked ? (int?)numLowpass.Value : null,
            SpeechNorm = chkSpeechNorm.Checked
        };

        void SavePreset()
        {
            var name = Prompt(Localizer.Tr("Preset name:"));
            if (string.IsNullOrWhiteSpace(name)) return;
            if (DefaultPresets.ContainsKey(name) || _settings.CustomPresets.ContainsKey(name))
            {
                MessageBox.Show(this, Localizer.Tr("Preset exists"), Localizer.Tr("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            _settings.CustomPresets[name] = BuildProfileFromUi();
            _settings.Save(_settingsPath);
            LoadPresetsToUi();
            cboPreset.SelectedItem = name;
        }

        void RenamePreset()
        {
            if (cboPreset.SelectedItem is not string oldName) return;
            if (DefaultPresets.ContainsKey(oldName))
            {
                MessageBox.Show(this, Localizer.Tr("Cannot rename builtin preset"), Localizer.Tr("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!_settings.CustomPresets.TryGetValue(oldName, out var prof)) return;
            var newName = Prompt(Localizer.Tr("Preset name:"), oldName);
            if (string.IsNullOrWhiteSpace(newName) || newName == oldName) return;
            if (DefaultPresets.ContainsKey(newName) || _settings.CustomPresets.ContainsKey(newName))
            {
                MessageBox.Show(this, Localizer.Tr("Preset exists"), Localizer.Tr("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            _settings.CustomPresets.Remove(oldName);
            _settings.CustomPresets[newName] = prof;
            _settings.Save(_settingsPath);
            LoadPresetsToUi();
            cboPreset.SelectedItem = newName;
        }

        void DeletePreset()
        {
            if (cboPreset.SelectedItem is not string name) return;
            if (DefaultPresets.ContainsKey(name))
            {
                MessageBox.Show(this, Localizer.Tr("Cannot delete builtin preset"), Localizer.Tr("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (_settings.CustomPresets.Remove(name))
            {
                _settings.Save(_settingsPath);
                LoadPresetsToUi();
            }
        }

        bool ProfilesEqual(DenoiseProfile a, DenoiseProfile b) =>
            a.Mix == b.Mix && a.HighpassHz == b.HighpassHz && a.LowpassHz == b.LowpassHz && a.SpeechNorm == b.SpeechNorm;

        void SelectPresetFromProfile(DenoiseProfile p)
        {
            foreach (var kv in DefaultPresets.Concat(_settings.CustomPresets))
            {
                if (ProfilesEqual(p, kv.Value))
                {
                    cboPreset.SelectedItem = kv.Key;
                    return;
                }
            }
            cboPreset.SelectedIndex = -1;
        }

        void ShowReadmeIfNeeded()
        {
            if (_settings.ShowReadme) ShowReadme();
        }

        void ShowReadme()
        {
            using var frm = new ReadmeForm();
            if (frm.ShowDialog(this) == DialogResult.OK && frm.DontShow)
            {
                _settings.ShowReadme = false;
                _settings.Save(_settingsPath);
            }
        }

        string? Prompt(string text, string defaultValue = "")
        {
            using var form = new Form { Width = 400, Height = 140, FormBorderStyle = FormBorderStyle.FixedDialog, Text = text, StartPosition = FormStartPosition.CenterParent };
            var tb = new TextBox { Left = 10, Top = 10, Width = 360, Text = defaultValue };
            var ok = new Button { Text = Localizer.Tr("OK"), Left = 210, Width = 80, Top = 40, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = Localizer.Tr("Cancel"), Left = 300, Width = 80, Top = 40, DialogResult = DialogResult.Cancel };
            form.Controls.Add(tb);
            form.Controls.Add(ok);
            form.Controls.Add(cancel);
            form.AcceptButton = ok;
            form.CancelButton = cancel;
            return form.ShowDialog(this) == DialogResult.OK ? tb.Text : null;
        }

        private void chkHighpass_CheckedChanged(object sender, EventArgs e)
        {
            numHighpass.Enabled = chkHighpass.Checked;
        }

        private void chkLowpass_CheckedChanged(object sender, EventArgs e)
        {
            numLowpass.Enabled = chkLowpass.Checked;
        }
    }

    // ===== Settings & models =====

    public sealed class DenoiseProfile
    {
        public double Mix { get; set; } = 0.85;
        public int? HighpassHz { get; set; } = null;
        public int? LowpassHz { get; set; } = null;
        public bool SpeechNorm { get; set; } = false;
    }

    public sealed class AppSettings
    {
        public string FfmpegBinPath { get; set; } = "";
        public string ModelPath { get; set; } = "";
        public string OutputFolder { get; set; } =
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        public DenoiseProfile Profile { get; set; } = new();
        public string AudioCodec { get; set; } = "aac";
        public string AudioBitrate { get; set; } = "192k";
        public bool CopyVideo { get; set; } = true;
        public string Language { get; set; } = "";
        public bool ShowReadme { get; set; } = true;
        public Dictionary<string, DenoiseProfile> CustomPresets { get; set; } = new();

        public static AppSettings Load(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var s = File.ReadAllText(path, Encoding.UTF8);
                    var obj = JsonSerializer.Deserialize<AppSettings>(s);
                    if (obj != null)
                    {
                        obj.CustomPresets ??= new();
                        return obj;
                    }
                }
            }
            catch { }
            return new AppSettings();
        }

        public void Save(string path)
        {
            try
            {
                var s = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, s, Encoding.UTF8);
            }
            catch { }
        }
    }

    public sealed class QueueItem
    {
        public string Input { get; set; } = string.Empty;
        public string Output { get; set; } = string.Empty;
    }

    public sealed class LangItem
    {
        public string Code { get; }
        public string Name { get; }
        public LangItem(string code, string name)
        {
            Code = code;
            Name = name;
        }
        public override string ToString() => Name;
    }

    public static class Localizer
    {
        public static string Current { get; private set; } = "en";
        public static readonly LangItem[] Langs = new[]
        {
            new LangItem("en", "English"),
            new LangItem("ru", "Русский"),
            new LangItem("pt", "Português"),
            new LangItem("es", "Español"),
            new LangItem("de", "Deutsch"),
            new LangItem("fr", "Français"),
            new LangItem("tr", "Türkçe"),
            new LangItem("pl", "Polski"),
            new LangItem("ja", "日本語"),
            new LangItem("ko", "한국어"),
            new LangItem("it", "Italiano"),
            new LangItem("uk", "Українська"),
            new LangItem("cs", "Česky"),
            new LangItem("sk", "Slovenčina"),
            new LangItem("ro", "Română"),
            new LangItem("nl", "Nederlands"),
            new LangItem("sr", "Srpski/Hrvatski/Bosanski/Crnogorski"),
        };

        static Dictionary<string, Dictionary<string, string>> Data = new();

        static Localizer()
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "translations.json");
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    Data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json) ?? new();
                }
            }
            catch
            {
                Data = new();
            }
        }

        public static void Set(string code) => Current = code;
        public static string Tr(string key)
        {
            if (Data.TryGetValue(Current, out var d) && d.TryGetValue(key, out var v)) return v;
            return key;
        }
    }

}
