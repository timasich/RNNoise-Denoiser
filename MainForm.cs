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

            // minor toggles
            chkHighpass.CheckedChanged += (s, e) => numHighpass.Enabled = chkHighpass.Checked;
            chkLowpass.CheckedChanged += (s, e) => numLowpass.Enabled = chkLowpass.Checked;
            // Settings path
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "RNNoiseDenoiser");
            Directory.CreateDirectory(dir);
            _settingsPath = Path.Combine(dir, "settings.json");
            _settings = AppSettings.Load(_settingsPath);

            // Defaults to your paths (editable in UI)
            if (string.IsNullOrWhiteSpace(_settings.FfmpegBinPath))
                _settings.FfmpegBinPath = @"C:\Tools\ffmpeg\bin";
            if (string.IsNullOrWhiteSpace(_settings.ModelPath))
                _settings.ModelPath = @"C:\Tools\rnnoise\models\sh.rnnn";
            if (string.IsNullOrWhiteSpace(_settings.OutputFolder))
                _settings.OutputFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            LoadSettingsToUi();
            WireEvents();
            InitTooltips();

            AllowDrop = true;
            DragEnter += (s, e) => { if (e.Data!.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragDrop += (s, e) => EnqueueFiles((string[])e.Data!.GetData(DataFormats.FileDrop)!);
        }

        void WireEvents()
        {
            btnFfmpegBrowse.Click += (s, e) =>
            {
                using var fbd = new FolderBrowserDialog { Description = "Папка с ffmpeg/bin (ffmpeg.exe, ffprobe.exe)" };
                if (fbd.ShowDialog(this) == DialogResult.OK) txtFfmpeg.Text = fbd.SelectedPath;
            };
            btnModelBrowse.Click += (s, e) =>
            {
                using var ofd = new OpenFileDialog { Filter = "RNNoise model (*.rnnn)|*.rnnn|All files|*.*" };
                if (ofd.ShowDialog(this) == DialogResult.OK) txtModel.Text = ofd.FileName;
            };
            btnOutputBrowse.Click += (s, e) =>
            {
                using var fbd = new FolderBrowserDialog { Description = "Папка вывода" };
                if (fbd.ShowDialog(this) == DialogResult.OK) txtOutput.Text = fbd.SelectedPath;
            };
            btnAddFiles.Click += (s, e) =>
            {
                using var ofd = new OpenFileDialog
                {
                    Filter = "Видео/Аудио|*.mp4;*.mov;*.mkv;*.m4v;*.avi;*.webm;*.wav;*.mp3;*.m4a;*.aac;*.flac;*.ogg|Все файлы|*.*",
                    Multiselect = true
                };
                if (ofd.ShowDialog(this) == DialogResult.OK) EnqueueFiles(ofd.FileNames);
            };
            btnAddFolder.Click += (s, e) =>
            {
                using var fbd = new FolderBrowserDialog { Description = "Папка с файлами" };
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

            var ctx = new ContextMenuStrip();
            ctx.Items.Add("Удалить из очереди", null, (s, e) =>
            {
                foreach (ListViewItem it in lvQueue.SelectedItems.Cast<ListViewItem>().ToArray())
                    lvQueue.Items.Remove(it);
            });
            ctx.Items.Add("Дублировать", null, (s, e) =>
            {
                foreach (ListViewItem it in lvQueue.SelectedItems.Cast<ListViewItem>().ToArray())
                {
                    var qi = (QueueItem)it.Tag!;
                    var copy = new ListViewItem(new[] { "", qi.Input, "В очереди", "0%", "", "" })
                    { Tag = new QueueItem { Input = qi.Input }, Checked = true };
                    lvQueue.Items.Add(copy);
                }
            });
            ctx.Items.Add("Открыть папку с оригиналом", null, (s, e) =>
            {
                if (lvQueue.SelectedItems.Count == 0) return;
                var qi = (QueueItem)lvQueue.SelectedItems[0].Tag!;
                try { Process.Start("explorer.exe", $"/select,\"{qi.Input}\""); } catch { }
            });
            ctx.Items.Add("Открыть папку вывода", null, (s, e) =>
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
            ctx.Items.Add("Отметить для очистки", null, (s, e) =>
            {
                foreach (ListViewItem it in lvQueue.SelectedItems.Cast<ListViewItem>())
                    it.Checked = true;
            });
            ctx.Items.Add("Снять из очистки", null, (s, e) =>
            {
                foreach (ListViewItem it in lvQueue.SelectedItems.Cast<ListViewItem>())
                    it.Checked = false;
            });
            lvQueue.ContextMenuStrip = ctx;
        }

        void InitTooltips()
        {
            _tip.SetToolTip(txtFfmpeg, "Папка с ffmpeg.exe и ffprobe.exe");
            _tip.SetToolTip(btnFfmpegBrowse, "Выбрать папку ffmpeg/bin");
            _tip.SetToolTip(txtModel, "Файл модели RNNoise (.rnnn)");
            _tip.SetToolTip(btnModelBrowse, "Выбрать файл модели RNNoise");
            _tip.SetToolTip(txtOutput, "Папка для сохранения обработанных файлов");
            _tip.SetToolTip(btnOutputBrowse, "Выбрать папку вывода");
            _tip.SetToolTip(cboAudioCodec, "Кодек аудио выходного файла");
            _tip.SetToolTip(cboBitrate, "Битрейт аудио: выше — лучше качество, но больше размер файла");
            _tip.SetToolTip(numMix, "Степень шумоподавления: 0 — без обработки, 1 — максимальное подавление");
            _tip.SetToolTip(chkHighpass, "Удаляет частоты ниже указанной, помогает убрать гул");
            _tip.SetToolTip(numHighpass, "Граница фильтра высоких частот (Гц)");
            _tip.SetToolTip(chkLowpass, "Удаляет частоты выше указанной, подавляет ВЧ-шумы");
            _tip.SetToolTip(numLowpass, "Граница фильтра низких частот (Гц)");
            _tip.SetToolTip(chkSpeechNorm, "Выравнивает громкость речи");
            _tip.SetToolTip(chkCopyVideo, "Не перекодировать видео, только заменять аудио");
            _tip.SetToolTip(btnAddFiles, "Добавить файлы в очередь");
            _tip.SetToolTip(btnAddFolder, "Добавить все поддерживаемые файлы из папки");
            _tip.SetToolTip(btnStart, "Начать обработку отмеченных файлов");
            _tip.SetToolTip(btnCancel, "Отменить текущую обработку");
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
            _settings.Save(_settingsPath);
        }

        void EnqueueFiles(IEnumerable<string> files)
        {
            int added = 0;
            foreach (var f in files.Distinct())
            {
                var it = new ListViewItem(new[] { "", f, "В очереди", "0%", "", "" })
                { Tag = new QueueItem { Input = f }, Checked = true };
                lvQueue.Items.Add(it);
                added++;
            }
            tslStatus.Text = $"Добавлено файлов: {added}";
        }

        async Task StartAsync()
        {
            SaveSettingsFromUi();
            if (!ValidatePaths()) return;

            btnStart.Enabled = false;
            btnCancel.Enabled = true;
            _cts = new CancellationTokenSource();
            tslStatus.Text = "Обработка…";

            try
            {
                foreach (ListViewItem it in lvQueue.Items)
                {
                    if (_cts.IsCancellationRequested) break;
                    if (!it.Checked) continue;

                    var qi = (QueueItem)it.Tag!;
                    it.SubItems[2].Text = "Подготовка";
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
                            tslStatus.Text = $"Осталось {rem:hh\\:mm\\:ss}";
                        })),
                        _cts.Token);
                    sw.Stop();

                    BeginInvoke(new Action(() =>
                    {
                        it.SubItems[2].Text = ok ? "Готово" : ("Ошибка: " + err);
                        if (ok) it.SubItems[3].Text = "100%";
                        it.SubItems[4].Text = sw.Elapsed.ToString("hh\\:mm\\:ss");
                        if (ok) it.Checked = false;
                        tslStatus.Text = ok ? $"Время: {sw.Elapsed:hh\\:mm\\:ss}" : "Готов";
                    }));
                }
            }
            finally
            {
                btnStart.Enabled = true;
                btnCancel.Enabled = false;
                _cts = null;
                tslStatus.Text = "Готов";
            }
        }

        bool ValidatePaths()
        {
            string ffmpeg = Path.Combine(txtFfmpeg.Text.Trim(), "ffmpeg.exe");
            string ffprobe = Path.Combine(txtFfmpeg.Text.Trim(), "ffprobe.exe");
            if (!File.Exists(ffmpeg) || !File.Exists(ffprobe))
            {
                MessageBox.Show(this, "Не найден ffmpeg.exe/ffprobe.exe. Укажи путь к папке bin.", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            if (!File.Exists(txtModel.Text.Trim()))
            {
                MessageBox.Show(this, "Не найден файл модели .rnnn.", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            if (string.IsNullOrWhiteSpace(txtOutput.Text) || !Directory.Exists(txtOutput.Text))
            {
                MessageBox.Show(this, "Укажи существующую папку вывода.", "Ошибка",
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
                if (ct.IsCancellationRequested) return (false, "Отменено");
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
                // Заменяем обратные слеши на прямые и экранируем двоеточие
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

}
