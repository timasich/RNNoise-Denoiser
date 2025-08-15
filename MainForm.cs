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
            btnAddFiles.Text = Localizer.Tr("Add files");
            btnAddFolder.Text = Localizer.Tr("Add folder");
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
            _tip.SetToolTip(btnStart, Localizer.Tr("Start processing selected files"));
            _tip.SetToolTip(btnCancel, Localizer.Tr("Cancel current processing"));

            BuildContextMenu();
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
            string model = txtModel.Text.Trim();

            var filters = new List<string>();
            if (chkHighpass.Checked) filters.Add($"highpass=f={(int)numHighpass.Value}");
            if (chkLowpass.Checked) filters.Add($"lowpass=f={(int)numLowpass.Value}");
            filters.Add("aresample=48000");
            filters.Add($"arnndn=m='{ModelPathForFfmpeg(model)}':mix={((double)numMix.Value).ToString(CultureInfo.InvariantCulture)}");
            if (chkSpeechNorm.Checked) filters.Add("speechnorm=e=6");
            string filterChain = string.Join(',', filters);

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
            static string ModelPathForFfmpeg(string path)
            {
                // Replace backslashes with forward slashes and escape colon
                return path.Replace("\\", "/").Replace(":", "\\:");
            }

        }

        static string EscapeForFilter(string path) => path.Replace("\\", "\\\\");

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
        public string Language { get; set; } = "en";

        public static AppSettings Load(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var s = File.ReadAllText(path, Encoding.UTF8);
                    var obj = JsonSerializer.Deserialize<AppSettings>(s);
                    if (obj != null) return obj;
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

        static readonly Dictionary<string, Dictionary<string, string>> Data = new()
        {
            ["en"] = new(),
            ["ru"] = new()
            {
                ["FFmpeg bin:"] = "Папка ffmpeg:",
                ["Browse"] = "Обзор",
                ["RNNoise .rnnn:"] = "Модель RNNoise (.rnnn):",
                ["Output folder:"] = "Папка вывода:",
                ["Audio codec:"] = "Кодек аудио:",
                ["Bitrate:"] = "Битрейт:",
                ["mix (0-1):"] = "mix (0–1):",
                ["Highpass (Hz):"] = "Highpass (Гц):",
                ["Lowpass (Hz):"] = "Lowpass (Гц):",
                ["SpeechNorm:"] = "SpeechNorm:",
                ["Copy video:"] = "Копировать видео:",
                ["On"] = "Вкл",
                ["Add files"] = "Добавить файлы",
                ["Add folder"] = "Добавить папку",
                ["Start"] = "Старт",
                ["Cancel"] = "Отмена",
                ["File"] = "Файл",
                ["Status"] = "Статус",
                ["Progress"] = "Прогресс",
                ["Time"] = "Время",
                ["Output"] = "Выход",
                ["Ready"] = "Готов",
                ["Processing..."] = "Обработка…",
                ["Remaining {0}"] = "Осталось {0}",
                ["Time: {0}"] = "Время: {0}",
                ["Queued"] = "В очереди",
                ["Preparing"] = "Подготовка",
                ["Done"] = "Готово",
                ["Error:"] = "Ошибка:",
                ["Added files: {0}"] = "Добавлено файлов: {0}",
                ["Remove from queue"] = "Удалить из очереди",
                ["Duplicate"] = "Дублировать",
                ["Open folder with original"] = "Открыть папку с оригиналом",
                ["Open output folder"] = "Открыть папку вывода",
                ["Mark for cleanup"] = "Отметить для очистки",
                ["Unmark from cleanup"] = "Снять из очистки",
                ["ffmpeg.exe/ffprobe.exe not found. Specify path to bin folder."] = "Не найден ffmpeg.exe/ffprobe.exe. Укажи путь к папке bin.",
                [".rnnn model file not found."] = "Не найден файл модели .rnnn.",
                ["Specify an existing output folder."] = "Укажи существующую папку вывода.",
                ["Error"] = "Ошибка",
                ["Folder with ffmpeg.exe and ffprobe.exe"] = "Папка с ffmpeg.exe и ffprobe.exe",
                ["Select ffmpeg/bin folder"] = "Выбрать папку ffmpeg/bin",
                ["RNNoise model file (.rnnn)"] = "Файл модели RNNoise (.rnnn)",
                ["Select RNNoise model file"] = "Выбрать файл модели RNNoise",
                ["Folder to save processed files"] = "Папка для сохранения обработанных файлов",
                ["Select output folder"] = "Выбрать папку вывода",
                ["Audio codec of output file"] = "Кодек аудио выходного файла",
                ["Audio bitrate: higher = better quality but larger file"] = "Битрейт аудио: выше — лучше качество, но больше размер файла",
                ["Noise reduction amount: 0 = no processing, 1 = maximum reduction"] = "Степень шумоподавления: 0 — без обработки, 1 — максимальное подавление",
                ["Removes frequencies below specified value, helps remove hum"] = "Удаляет частоты ниже указанной, помогает убрать гул",
                ["High-pass filter cutoff frequency (Hz)"] = "Граница фильтра высоких частот (Гц)",
                ["Removes frequencies above specified value, suppresses HF noise"] = "Удаляет частоты выше указанной, подавляет ВЧ-шумы",
                ["Low-pass filter cutoff frequency (Hz)"] = "Граница фильтра низких частот (Гц)",
                ["Normalizes speech loudness"] = "Выравнивает громкость речи",
                ["Do not re-encode video, replace audio only"] = "Не перекодировать видео, только заменять аудио",
                ["Add files to queue"] = "Добавить файлы в очередь",
                ["Add all supported files from folder"] = "Добавить все поддерживаемые файлы из папки",
                ["Start processing selected files"] = "Начать обработку отмеченных файлов",
                ["Cancel current processing"] = "Отменить текущую обработку",
                ["Video/Audio"] = "Видео/Аудио",
                ["All files"] = "Все файлы",
                ["Folder with ffmpeg/bin (ffmpeg.exe, ffprobe.exe)"] = "Папка с ffmpeg/bin (ffmpeg.exe, ffprobe.exe)",
                ["RNNoise model file"] = "Файл модели RNNoise",
                ["Folder with files"] = "Папка с файлами",
                ["Output folder"] = "Папка вывода",
                ["Cancelled"] = "Отменено"
            }
        };

        public static void Set(string code) => Current = code;
        public static string Tr(string key)
        {
            if (Data.TryGetValue(Current, out var d) && d.TryGetValue(key, out var v)) return v;
            return key;
        }
    }

}
