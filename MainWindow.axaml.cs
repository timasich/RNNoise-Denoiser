using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RNNoise_Denoiser;

public partial class MainWindow : Window
{
    readonly AppSettings _settings;
    readonly string _settingsPath;
    readonly ObservableCollection<QueueItem> _queue = new();
    readonly Dictionary<string, DenoiseProfile> _builtInPresets = new()
    {
        ["Soft"] = new DenoiseProfile { Mix = 0.90 },
        ["Standard"] = new DenoiseProfile { Mix = 0.85 },
        ["Aggressive"] = new DenoiseProfile { Mix = 0.70, HighpassHz = 80, LowpassHz = 12000 },
    };
    readonly string[] _extensions = { ".wav", ".mp3", ".flac", ".m4a", ".ogg", ".mp4", ".mkv", ".avi" };
    CancellationTokenSource? _cts;

    public MainWindow()
    {
        InitializeComponent();

        numMix.ValueChanged += (s, e) =>
        {
            var tb = s as TextBox;
            if (tb == null) return;
            var caret = tb.CaretIndex;
            var newText = ToInvariantNumber(tb.Text);
            if (tb.Text != newText)
            {
                tb.Text = newText;
                tb.CaretIndex = Math.Min(caret, tb.Text.Length);
            }
        };

        numHighpass.ValueChanged += (s, e) =>
        {
            var tb = s as TextBox;
            if (tb == null) return;
            var caret = tb.CaretIndex;
            var newText = ToInvariantNumber(tb.Text);
            if (tb.Text != newText)
            {
                tb.Text = newText;
                tb.CaretIndex = Math.Min(caret, tb.Text.Length);
            }
        };

        numLowpass.ValueChanged += (s, e) =>
        {
            var tb = s as TextBox;
            if (tb == null) return;
            var caret = tb.CaretIndex;
            var newText = ToInvariantNumber(tb.Text);
            if (tb.Text != newText)
            {
                tb.Text = newText;
                tb.CaretIndex = Math.Min(caret, tb.Text.Length);
            }
        };

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "RNNoiseDenoiser");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
        _settings = AppSettings.Load(_settingsPath);

        foreach (var l in Localizer.Langs)
            cboLang.Items.Add(l);
        var li = Localizer.Langs.FirstOrDefault(l => l.Code == _settings.Language) ?? Localizer.Langs[0];
        cboLang.SelectedItem = li;
        Localizer.Set(li.Code);

        ApplyLocalization();
        LoadSettingsToUi();
        LoadPresets();

        cboLang.SelectionChanged += (_, __) =>
        {
            if (cboLang.SelectedItem is LangItem lang)
            {
                Localizer.Set(lang.Code);
                ApplyLocalization();
            }
        };

        chkHighpass.Checked += (_, __) => numHighpass.IsEnabled = true;
        chkHighpass.Unchecked += (_, __) => numHighpass.IsEnabled = false;
        chkLowpass.Checked += (_, __) => numLowpass.IsEnabled = true;
        chkLowpass.Unchecked += (_, __) => numLowpass.IsEnabled = false;

        btnFfmpegBrowse.Click += BtnFfmpegBrowse_Click;
        btnModelBrowse.Click += BtnModelBrowse_Click;
        btnOutputBrowse.Click += BtnOutputBrowse_Click;
        btnAddFiles.Click += BtnAddFiles_Click;
        btnAddFolder.Click += BtnAddFolder_Click;
        btnCheckEnv.Click += BtnCheckEnv_Click;
        btnPreview.Click += BtnPreview_Click;
        btnStart.Click += BtnStart_Click;
        btnCancel.Click += BtnCancel_Click;
        btnSavePreset.Click += BtnSavePreset_Click;
        btnRenamePreset.Click += BtnRenamePreset_Click;
        btnDeletePreset.Click += BtnDeletePreset_Click;

        dgQueue.ItemsSource = _queue;

        dgQueue.AddHandler(DragDrop.DropEvent, OnDrop);
        dgQueue.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);

        miRemove.Click += MiRemove_Click;
        miDuplicate.Click += MiDuplicate_Click;
        miOpenOriginal.Click += MiOpenOriginal_Click;
        miOpenOutput.Click += MiOpenOutput_Click;
        miMark.Click += MiMark_Click;
        miUnmark.Click += MiUnmark_Click;
        miPreview.Click += MiPreview_Click;

        tslMadeBy.PointerPressed += (_, __) => ShowReadme();
        if (_settings.ShowReadme)
            ShowReadme();
    }

    async void BtnFfmpegBrowse_Click(object? sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog();
        var res = await dlg.ShowAsync(this);
        if (!string.IsNullOrEmpty(res))
            txtFfmpeg.Text = res;
    }

    async void BtnModelBrowse_Click(object? sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog();
        dlg.Filters.Add(new FileDialogFilter { Name = "RNNoise model", Extensions = { "rnnn" } });
        var res = await dlg.ShowAsync(this);
        if (res != null && res.Length > 0)
            txtModel.Text = res[0];
    }

    async void BtnOutputBrowse_Click(object? sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog();
        var res = await dlg.ShowAsync(this);
        if (!string.IsNullOrEmpty(res))
            txtOutput.Text = res;
    }

    void LoadSettingsToUi()
    {
        txtFfmpeg.Text = _settings.FfmpegBinPath;
        txtModel.Text = _settings.ModelPath;
        txtOutput.Text = _settings.OutputFolder;

        cboAudioCodec.Items.Clear();
        cboAudioCodec.Items.Add("aac");
        cboAudioCodec.Items.Add("libmp3lame");
        cboAudioCodec.Items.Add("pcm_s16le");
        cboAudioCodec.SelectedItem = _settings.AudioCodec;

        cboBitrate.Items.Clear();
        foreach (var br in new[] { "128k", "160k", "192k", "256k", "320k" })
            cboBitrate.Items.Add(br);
        cboBitrate.SelectedItem = _settings.AudioBitrate;

        ApplyProfile(_settings.Profile);
        chkCopyVideo.IsChecked = _settings.CopyVideo;
    }

    void SaveSettingsFromUi()
    {
        _settings.FfmpegBinPath = txtFfmpeg.Text;
        _settings.ModelPath = txtModel.Text;
        _settings.OutputFolder = txtOutput.Text;
        _settings.AudioCodec = (string?)cboAudioCodec.SelectedItem ?? "aac";
        _settings.AudioBitrate = (string?)cboBitrate.SelectedItem ?? "192k";
        _settings.Profile = ProfileFromUi();
        _settings.CopyVideo = chkCopyVideo.IsChecked == true;
        _settings.Language = (cboLang.SelectedItem as LangItem)?.Code ?? "en";
        _settings.Save(_settingsPath);
    }

    protected override void OnClosed(EventArgs e)
    {
        SaveSettingsFromUi();
        base.OnClosed(e);
    }

    void ApplyLocalization()
    {
        lblFfmpeg.Text = Localizer.Tr("FFmpeg bin:");
        lblModel.Text = Localizer.Tr("Model:");
        lblOutput.Text = Localizer.Tr("Output:");
        btnFfmpegBrowse.Content = Localizer.Tr("Browse");
        btnModelBrowse.Content = Localizer.Tr("Browse");
        btnOutputBrowse.Content = Localizer.Tr("Browse");
        btnAddFiles.Content = Localizer.Tr("Add files");
        btnAddFolder.Content = Localizer.Tr("Add folder");
        btnCheckEnv.Content = Localizer.Tr("Check env");
        btnPreview.Content = Localizer.Tr("Preview");
        btnStart.Content = Localizer.Tr("Start");
        btnCancel.Content = Localizer.Tr("Cancel");
        lblPreset.Text = Localizer.Tr("Preset:");
        btnSavePreset.Content = Localizer.Tr("Save");
        btnRenamePreset.Content = Localizer.Tr("Rename");
        btnDeletePreset.Content = Localizer.Tr("Delete");
        lblCodec.Text = Localizer.Tr("Codec:");
        lblBr.Text = Localizer.Tr("Bitrate:");
        lblMix.Text = Localizer.Tr("mix (0-1):");
        lblHp.Text = Localizer.Tr("Highpass (Hz):");
        lblSn.Text = Localizer.Tr("SpeechNorm:");
        lblLp.Text = Localizer.Tr("Lowpass (Hz):");
        lblCopy.Text = Localizer.Tr("Copy video:");
        ToolTip.SetTip(lblFfmpeg, Localizer.Tr("Folder with ffmpeg/bin (ffmpeg.exe, ffprobe.exe)"));
        ToolTip.SetTip(txtFfmpeg, Localizer.Tr("Folder with ffmpeg/bin (ffmpeg.exe, ffprobe.exe)"));
        ToolTip.SetTip(btnFfmpegBrowse, Localizer.Tr("Select ffmpeg/bin folder"));
        ToolTip.SetTip(lblModel, Localizer.Tr("RNNoise model file"));
        ToolTip.SetTip(txtModel, Localizer.Tr("RNNoise model file"));
        ToolTip.SetTip(btnModelBrowse, Localizer.Tr("Select RNNoise model file"));
        ToolTip.SetTip(lblOutput, Localizer.Tr("Output folder"));
        ToolTip.SetTip(txtOutput, Localizer.Tr("Output folder"));
        ToolTip.SetTip(btnOutputBrowse, Localizer.Tr("Select output folder"));
        ToolTip.SetTip(btnAddFiles, Localizer.Tr("Add files to queue"));
        ToolTip.SetTip(btnAddFolder, Localizer.Tr("Add all supported files from folder"));
        ToolTip.SetTip(btnCheckEnv, Localizer.Tr("Check required tools"));
        ToolTip.SetTip(btnPreview, Localizer.Tr("Preview selected file"));
        ToolTip.SetTip(btnStart, Localizer.Tr("Start processing selected files"));
        ToolTip.SetTip(btnCancel, Localizer.Tr("Cancel current processing"));
        ToolTip.SetTip(lblCodec, Localizer.Tr("Audio codec of output file"));
        ToolTip.SetTip(cboAudioCodec, Localizer.Tr("Audio codec of output file"));
        ToolTip.SetTip(lblBr, Localizer.Tr("Audio bitrate: higher = better quality but larger file"));
        ToolTip.SetTip(cboBitrate, Localizer.Tr("Audio bitrate: higher = better quality but larger file"));
        ToolTip.SetTip(lblMix, Localizer.Tr("Noise reduction amount: 0 = no processing, 1 = maximum reduction"));
        ToolTip.SetTip(numMix, Localizer.Tr("Noise reduction amount: 0 = no processing, 1 = maximum reduction"));
        ToolTip.SetTip(lblHp, Localizer.Tr("Removes frequencies below specified value, helps remove hum"));
        ToolTip.SetTip(chkHighpass, Localizer.Tr("Removes frequencies below specified value, helps remove hum"));
        ToolTip.SetTip(numHighpass, Localizer.Tr("High-pass filter cutoff frequency (Hz)"));
        ToolTip.SetTip(lblSn, Localizer.Tr("Normalizes speech loudness"));
        ToolTip.SetTip(chkSpeechNorm, Localizer.Tr("Normalizes speech loudness"));
        ToolTip.SetTip(lblLp, Localizer.Tr("Removes frequencies above specified value, suppresses HF noise"));
        ToolTip.SetTip(chkLowpass, Localizer.Tr("Removes frequencies above specified value, suppresses HF noise"));
        ToolTip.SetTip(numLowpass, Localizer.Tr("Low-pass filter cutoff frequency (Hz)"));
        ToolTip.SetTip(lblCopy, Localizer.Tr("Do not re-encode video, replace audio only"));
        ToolTip.SetTip(chkCopyVideo, Localizer.Tr("Do not re-encode video, replace audio only"));
        ToolTip.SetTip(lblPreset, Localizer.Tr("Preset:"));
        ToolTip.SetTip(cboPreset, Localizer.Tr("Preset:"));
        ToolTip.SetTip(btnSavePreset, Localizer.Tr("Save"));
        ToolTip.SetTip(btnRenamePreset, Localizer.Tr("Rename"));
        ToolTip.SetTip(btnDeletePreset, Localizer.Tr("Delete"));
        if (dgQueue.Columns.Count > 5)
        {
            dgQueue.Columns[1].Header = Localizer.Tr("File");
            dgQueue.Columns[2].Header = Localizer.Tr("Status");
            dgQueue.Columns[3].Header = Localizer.Tr("Progress");
            dgQueue.Columns[4].Header = Localizer.Tr("Time");
            dgQueue.Columns[5].Header = Localizer.Tr("Output");
        }
        miRemove.Header = Localizer.Tr("Remove from queue");
        miDuplicate.Header = Localizer.Tr("Duplicate");
        miOpenOriginal.Header = Localizer.Tr("Open folder with original");
        miOpenOutput.Header = Localizer.Tr("Open output folder");
        miMark.Header = Localizer.Tr("Mark for cleanup");
        miUnmark.Header = Localizer.Tr("Unmark from cleanup");
        miPreview.Header = Localizer.Tr("Preview");
        tslStatus.Text = Localizer.Tr("Ready");
    }

    void LoadPresets()
    {
        cboPreset.Items.Clear();
        foreach (var p in _builtInPresets.Keys)
            cboPreset.Items.Add(p);
        foreach (var p in _settings.CustomPresets.Keys)
            cboPreset.Items.Add(p);
        cboPreset.SelectionChanged += (_, __) =>
        {
            if (cboPreset.SelectedItem is string name)
                ApplyProfile(GetPreset(name));
        };
        cboPreset.SelectedItem = "Standard";
    }

    DenoiseProfile GetPreset(string name)
    {
        if (_builtInPresets.TryGetValue(name, out var p)) return p;
        if (_settings.CustomPresets.TryGetValue(name, out var cp)) return cp;
        return new DenoiseProfile();
    }

    void ApplyProfile(DenoiseProfile p)
    {
        numMix.Value = (decimal)p.Mix;
        chkHighpass.IsChecked = p.HighpassHz.HasValue;
        numHighpass.Value = p.HighpassHz ?? 80;
        numHighpass.IsEnabled = p.HighpassHz.HasValue;
        chkLowpass.IsChecked = p.LowpassHz.HasValue;
        numLowpass.Value = p.LowpassHz ?? 12000;
        numLowpass.IsEnabled = p.LowpassHz.HasValue;
        chkSpeechNorm.IsChecked = p.SpeechNorm;
    }

    DenoiseProfile ProfileFromUi() => new()
    {
        Mix = (double)(numMix.Value ?? 0.85m),
        HighpassHz = chkHighpass.IsChecked == true ? (int?)numHighpass.Value : null,
        LowpassHz = chkLowpass.IsChecked == true ? (int?)numLowpass.Value : null,
        SpeechNorm = chkSpeechNorm.IsChecked == true,
    };

    void AddFilesToQueue(IEnumerable<string> files)
    {
        Directory.CreateDirectory(txtOutput.Text);
        foreach (var f in files)
        {
            if (Directory.Exists(f))
            {
                var dirFiles = Directory.GetFiles(f, "*.*", SearchOption.AllDirectories)
                    .Where(x => _extensions.Contains(Path.GetExtension(x).ToLowerInvariant()));
                foreach (var df in dirFiles)
                    AddFilesToQueue(new[] { df });
                continue;
            }
            if (!File.Exists(f)) continue;
            var output = Path.Combine(txtOutput.Text,
                Path.GetFileNameWithoutExtension(f) + "_denoised" + Path.GetExtension(f));
            _queue.Add(new QueueItem { Input = f, Output = output, IsChecked = true, Status = Localizer.Tr("Queued") });
        }
    }

    static string EscapeForFilterValue(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        // 1) нормализуем слеши
        var p = path.Trim().Replace("\\", "/");

        // 2) экранируем спецсимволы filtergraph:
        // ':' → '\:'  (иначе парсер решит, что это разделитель опций)
        // ''' → '\''  (если вдруг встретится в пути)
        p = p.Replace(":", "\\:")
             .Replace("'", "\\'");

        return p;
    }

    string BuildFilter()
    {
        var filters = new List<string>();

        if (chkHighpass.IsChecked == true && numHighpass.Value is decimal hp && hp > 0)
            filters.Add($"highpass=f={hp.ToString(CultureInfo.InvariantCulture)}");

        if (chkLowpass.IsChecked == true && numLowpass.Value is decimal lp && lp > 0)
            filters.Add($"lowpass=f={lp.ToString(CultureInfo.InvariantCulture)}");

        filters.Add("aresample=48000");

        // Путь к модели: экранируем под filtergraph и ОБОРАЧИВАЕМ в одинарные кавычки
        var modelEsc = EscapeForFilterValue(txtModel.Text ?? string.Empty);
        var arnndn = $"arnndn=m='{modelEsc}'";

        if (numMix.Value is decimal mv)
            arnndn += $":mix={((double)mv).ToString(CultureInfo.InvariantCulture)}";

        filters.Add(arnndn);

        if (chkSpeechNorm.IsChecked == true)
            filters.Add("speechnorm=e=6");

        return string.Join(",", filters);
    }

    string[] BuildFfmpegArgs(string input, string output)
    {
        var filter = BuildFilter();
        var list = new List<string>
        {
            "-i", input,
            "-af", filter,
            "-c:a", (string?)cboAudioCodec.SelectedItem ?? "aac",
            "-b:a", (string?)cboBitrate.SelectedItem ?? "192k"
        };
        if (chkCopyVideo.IsChecked == true)
            list.AddRange(new[] { "-c:v", "copy" });
        else
            list.Add("-vn");
        list.Add("-y");
        list.Add(output);
        return list.ToArray();
    }

    async void BtnAddFiles_Click(object? sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { AllowMultiple = true };
        dlg.Filters.Add(new FileDialogFilter { Name = "Media", Extensions = { "wav", "mp3", "flac", "m4a", "ogg", "mp4", "mkv", "avi" } });
        var res = await dlg.ShowAsync(this);
        if (res != null)
            AddFilesToQueue(res);
    }

    async void BtnAddFolder_Click(object? sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog();
        var folder = await dlg.ShowAsync(this);
        if (string.IsNullOrEmpty(folder)) return;
        var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories)
            .Where(f => _extensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
        AddFilesToQueue(files);
    }

    async void BtnCheckEnv_Click(object? sender, RoutedEventArgs e)
    {
        var ffmpeg = Path.Combine(txtFfmpeg.Text, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        var ffprobe = Path.Combine(txtFfmpeg.Text, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");
        var lines = new List<string>
        {
            $"ffmpeg: {(File.Exists(ffmpeg) ? Localizer.Tr("OK") : Localizer.Tr("Missing"))}",
            $"ffprobe: {(File.Exists(ffprobe) ? Localizer.Tr("OK") : Localizer.Tr("Missing"))}"
        };
        bool arnndn = false;
        if (File.Exists(ffmpeg))
        {
            try
            {
                var psi = new ProcessStartInfo(ffmpeg) { Arguments = "-hide_banner -filters", RedirectStandardOutput = true, UseShellExecute = false };
                var proc = Process.Start(psi);
                if (proc != null)
                {
                    var output = await proc.StandardOutput.ReadToEndAsync();
                    await proc.WaitForExitAsync();
                    arnndn = output.Contains("arnndn");
                }
            }
            catch { }
        }
        lines.Add($"{Localizer.Tr("arnndn filter")}: {(arnndn ? Localizer.Tr("OK") : Localizer.Tr("Missing"))}");
        lines.Add($"{Localizer.Tr("model")}: {(File.Exists(txtModel.Text) ? Localizer.Tr("OK") : Localizer.Tr("Missing"))}");

        var win = new Window
        {
            Width = 400,
            Height = 200,
            Title = Localizer.Tr("Environment"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var text = new TextBlock { Margin = new Thickness(10), Text = string.Join(Environment.NewLine, lines) };
        var ok = new Button { Content = Localizer.Tr("OK"), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10) };
        ok.Click += (_, __) => win.Close();
        var stack = new StackPanel();
        stack.Children.Add(text);
        stack.Children.Add(ok);
        win.Content = stack;
        await win.ShowDialog(this);
    }

    async void BtnPreview_Click(object? sender, RoutedEventArgs e)
    {
        var item = dgQueue.SelectedItem as QueueItem;
        if (item == null)
        {
            tslStatus.Text = Localizer.Tr("Select a file in queue");
            return;
        }
        await PreviewItem(item);
    }

    Task PreviewItem(QueueItem item)
    {
        var ffplay = Path.Combine(txtFfmpeg.Text, OperatingSystem.IsWindows() ? "ffplay.exe" : "ffplay");
        if (!File.Exists(ffplay))
        {
            tslStatus.Text = "ffplay not found";
            return Task.CompletedTask;
        }

        var filter = BuildFilter();

        try
        {
            var psi = new ProcessStartInfo(ffplay)
            {
                UseShellExecute = false
            };

            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(item.Input);

            psi.ArgumentList.Add("-af");
            psi.ArgumentList.Add(filter);

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => tslStatus.Text = ex.Message);
        }
        return Task.CompletedTask;
    }

    async void BtnStart_Click(object? sender, RoutedEventArgs e)
    {
        SaveSettingsFromUi();
        if (_queue.Count == 0) return;

        btnStart.IsEnabled = false;
        btnCancel.IsEnabled = true;
        _cts = new CancellationTokenSource();
        tslStatus.Text = Localizer.Tr("Processing...");

        var ffmpeg = Path.Combine(txtFfmpeg.Text,
            OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        var ffprobe = Path.Combine(txtFfmpeg.Text,
            OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");

        foreach (var item in _queue)
        {
            if (_cts.IsCancellationRequested)
            {
                item.Status = Localizer.Tr("Cancelled");
                break;
            }

            item.Status = Localizer.Tr("Preparing");
            var duration = await GetDurationAsync(ffprobe, item.Input);
            item.Progress = "0%";
            item.Time = FormatTime(0);
            item.Status = Localizer.Tr("Processing...");
            Directory.CreateDirectory(Path.GetDirectoryName(item.Output)!);

            var args = BuildFfmpegArgs(item.Input, item.Output);
            var filter = BuildFilter();

            try
            {
                var psi = new ProcessStartInfo(ffmpeg)
                {
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = false,
                    CreateNoWindow = true,
                    StandardErrorEncoding = Encoding.UTF8
                };

                foreach (var a in args)
                    psi.ArgumentList.Add(a);

                // вставляем фильтр отдельно
                psi.ArgumentList.Add("-af");
                psi.ArgumentList.Add(filter);

                var errLog = new StringBuilder(capacity: 64_000);
                using var proc = Process.Start(psi);

                if (proc == null)
                {
                    item.Status = "Error";
                    item.ErrorDetails = "Failed to start FFmpeg process.";
                    tslStatus.Text = "Failed to start FFmpeg.";
                    break;
                }

                // читаем STDERR
                var readErrTask = Task.Run(async () =>
                {
                    while (!proc.HasExited && !_cts!.IsCancellationRequested)
                    {
                        var line = await proc.StandardError.ReadLineAsync().ConfigureAwait(false);
                        if (line == null) break;
                        if (errLog.Length < 60_000) errLog.AppendLine(line);

                        var idx = line.IndexOf("time=");
                        if (idx >= 0 && duration > 0)
                        {
                            var tStr = line.Substring(idx + 5);
                            var space = tStr.IndexOf(' ');
                            if (space > 0) tStr = tStr[..space];
                            if (TimeSpan.TryParse(tStr, CultureInfo.InvariantCulture, out var ts))
                            {
                                var prog = ts.TotalSeconds / duration;
                                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                {
                                    item.Progress = prog.ToString("P0", CultureInfo.InvariantCulture);
                                    item.Time = FormatTime(ts.TotalSeconds);
                                    tslStatus.Text = string.Format(Localizer.Tr("Time: {0}"), FormatTime(ts.TotalSeconds));
                                });
                            }
                        }
                    }
                }, _cts.Token);

                await proc.WaitForExitAsync(_cts.Token).ConfigureAwait(false);
                await readErrTask.ConfigureAwait(false);

                if (proc.ExitCode == 0)
                {
                    item.Status = Localizer.Tr("Done");
                    item.Progress = "100%";
                    item.Time = FormatTime(duration);
                    item.IsChecked = false;
                }
                else
                {
                    var log = errLog.ToString();
                    var tail = LastLines(log, 12);
                    var hint = DiagnoseFfmpegError(log, proc.ExitCode, item.Input);

                    item.Status = $"Error ({proc.ExitCode})";
                    item.ErrorDetails = string.IsNullOrWhiteSpace(hint)
                        ? tail
                        : (hint + Environment.NewLine + Environment.NewLine + tail);
                    item.Progress = string.Empty;
                    item.Time = string.Empty;
                    tslStatus.Text = hint.Length > 0 ? hint : $"FFmpeg error {proc.ExitCode}";
                }
            }
            catch (OperationCanceledException)
            {
                item.Status = Localizer.Tr("Cancelled");
                break;
            }
            catch (Exception ex)
            {
                item.Status = "Error";
                item.ErrorDetails = ex.ToString();
                Avalonia.Threading.Dispatcher.UIThread.Post(() => tslStatus.Text = ex.Message);
                break;
            }
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(() => tslStatus.Text = Localizer.Tr("Ready"));
        Avalonia.Threading.Dispatcher.UIThread.Post(() => btnStart.IsEnabled = true);
        Avalonia.Threading.Dispatcher.UIThread.Post(() => btnCancel.IsEnabled = false);
        _cts = null;
    }

    static string FormatTime(double seconds) =>
        TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");

    async Task<double> GetDurationAsync(string ffprobe, string input)
    {
        try
        {
            var psi = new ProcessStartInfo(ffprobe)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-v");
            psi.ArgumentList.Add("error");
            psi.ArgumentList.Add("-show_entries");
            psi.ArgumentList.Add("format=duration");
            psi.ArgumentList.Add("-of");
            psi.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
            psi.ArgumentList.Add(input);
            using var proc = Process.Start(psi);
            if (proc == null) return 0;
            var output = await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await proc.WaitForExitAsync().ConfigureAwait(false);
            return double.TryParse(output.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;
        }
        catch
        {
            return 0;
        }
    }

    static string LastLines(string text, int lines)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var arr = text.Split('\n');
        int take = Math.Min(lines, arr.Length);
        return string.Join(Environment.NewLine, arr[^take..]).TrimEnd();
    }

    static string DiagnoseFfmpegError(string stderr, int exitCode, string inputPath)
    {
        stderr = stderr ?? string.Empty;
        string s = stderr.ToLowerInvariant();

        // Частые кейсы
        if (s.Contains("no such filter: 'arnndn'"))
            return "Your FFmpeg build lacks 'arnndn' filter. Install a build with arnndn support.";

        if (s.Contains("option 'mix' not found"))
            return "FFmpeg 'arnndn' in this build has no 'mix' option. Update FFmpeg or remove ':mix=' from the filter.";

        if (s.Contains("error opening model") || s.Contains("no such file or directory") && s.Contains("arnndn"))
            return "Model file not found or unreadable by 'arnndn'. Check the path; avoid single quotes and prefer forward slashes.";

        if (s.Contains("error initializing filter 'arnndn'") || s.Contains("invalid argument") || exitCode == -22)
            return "Invalid arguments for 'arnndn'. Most often: wrong model path or extra quotes in -af. Use m=/path/to/model.rnnn and do not wrap -af in quotes.";

        if (s.Contains("could not find codec parameters"))
            return "FFmpeg failed to read input. The file may be corrupt or unsupported.";

        if (s.Contains("protocol not found"))
            return "Unsupported protocol in path/URL. Use local file paths.";

        // Общая подсказка по путям
        if (s.Contains("no such file or directory"))
            return "Path not found. Verify FFmpeg path, model path and output folder.";

        // Если ничего не распознали — короткая справка
        return exitCode != 0
            ? $"FFmpeg returned {exitCode}. See error log tail below."
            : string.Empty;
    }

    void BtnCancel_Click(object? sender, RoutedEventArgs e) => _cts?.Cancel();

    async void BtnSavePreset_Click(object? sender, RoutedEventArgs e)
    {
        var name = await Prompt(Localizer.Tr("Preset name:"));
        if (string.IsNullOrWhiteSpace(name)) return;
        if (_builtInPresets.ContainsKey(name) || _settings.CustomPresets.ContainsKey(name))
        {
            tslStatus.Text = Localizer.Tr("Preset exists");
            return;
        }
        _settings.CustomPresets[name] = ProfileFromUi();
        cboPreset.Items.Add(name);
        cboPreset.SelectedItem = name;
    }

    async void BtnRenamePreset_Click(object? sender, RoutedEventArgs e)
    {
        if (cboPreset.SelectedItem is not string name) return;
        if (_builtInPresets.ContainsKey(name))
        {
            tslStatus.Text = Localizer.Tr("Cannot rename builtin preset");
            return;
        }
        var newName = await Prompt(Localizer.Tr("Preset name:"));
        if (string.IsNullOrWhiteSpace(newName)) return;
        if (_builtInPresets.ContainsKey(newName) || _settings.CustomPresets.ContainsKey(newName))
        {
            tslStatus.Text = Localizer.Tr("Preset exists");
            return;
        }
        var profile = _settings.CustomPresets[name];
        _settings.CustomPresets.Remove(name);
        _settings.CustomPresets[newName] = profile;
        var index = cboPreset.Items.IndexOf(name);
        cboPreset.Items[index] = newName;
        cboPreset.SelectedItem = newName;
    }

    void BtnDeletePreset_Click(object? sender, RoutedEventArgs e)
    {
        if (cboPreset.SelectedItem is not string name) return;
        if (_builtInPresets.ContainsKey(name))
        {
            tslStatus.Text = Localizer.Tr("Cannot delete builtin preset");
            return;
        }
        _settings.CustomPresets.Remove(name);
        cboPreset.Items.Remove(name);
        cboPreset.SelectedItem = "Standard";
    }

    async Task<string?> Prompt(string title)
    {
        var win = new Window
        {
            Width = 400,
            Height = 120,
            Title = title,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var tb = new TextBox { Margin = new Thickness(10) };
        var ok = new Button { Content = "OK", IsDefault = true };
        var cancel = new Button { Content = Localizer.Tr("Cancel"), IsCancel = true };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        var sp = new StackPanel();
        sp.Children.Add(tb);
        sp.Children.Add(buttons);
        win.Content = sp;
        var tcs = new TaskCompletionSource<string?>();
        ok.Click += (_, __) => { tcs.SetResult(tb.Text); win.Close(); };
        cancel.Click += (_, __) => { tcs.SetResult(null); win.Close(); };
        win.Closed += (_, __) => { if (!tcs.Task.IsCompleted) tcs.SetResult(null); };
        await win.ShowDialog(this);
        return await tcs.Task;
    }

    void MiRemove_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.DataContext is QueueItem item)
            _queue.Remove(item);
    }

    void MiDuplicate_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.DataContext is QueueItem item)
        {
            var clone = new QueueItem
            {
                Input = item.Input,
                Output = item.Output,
                IsChecked = item.IsChecked,
                Status = item.Status,
                Progress = item.Progress,
                Time = item.Time
            };
            var index = _queue.IndexOf(item);
            _queue.Insert(index + 1, clone);
        }
    }

    void MiOpenOriginal_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.DataContext is QueueItem item)
        {
            var dir = Path.GetDirectoryName(item.Input);
            if (!string.IsNullOrEmpty(dir))
                Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
    }

    void MiOpenOutput_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.DataContext is QueueItem item)
        {
            var dir = Path.GetDirectoryName(item.Output);
            if (!string.IsNullOrEmpty(dir))
                Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
    }

    void MiMark_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.DataContext is QueueItem item)
            item.IsChecked = true;
    }

    void MiUnmark_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.DataContext is QueueItem item)
            item.IsChecked = false;
    }

    async void MiPreview_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.DataContext is QueueItem item)
            await PreviewItem(item);
    }

    void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.FileNames))
            e.DragEffects = DragDropEffects.Copy;
    }

    void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.FileNames))
        {
            var files = e.Data.GetFileNames();
            AddFilesToQueue(files);
        }
    }

    async void ShowReadme()
    {
        var win = new ReadmeWindow { DontShowAgain = !_settings.ShowReadme };
        await win.ShowDialog(this);
        _settings.ShowReadme = !win.DontShowAgain;
        _settings.Save(_settingsPath);
    }

    private static string ToInvariantNumber(string input)
    {
        // Replace comma with dot, remove extra dots, allow only one dot
        var replaced = input.Replace(',', '.');
        int firstDot = replaced.IndexOf('.');
        if (firstDot >= 0)
        {
            // Remove all dots except the first
            replaced = replaced.Substring(0, firstDot + 1) +
                       replaced.Substring(firstDot + 1).Replace(".", "");
        }
        return replaced;
    }
}
