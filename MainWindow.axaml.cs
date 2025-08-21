using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
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
            _queue.Add(new QueueItem { Input = f, Output = output });
        }
    }

    string BuildFilter()
    {
        var filters = new List<string> { "aresample=48000" };
        var modelPath = txtModel.Text.Replace("\\", "/");
        var arnndn = $"arnndn=m='{modelPath}'";
        if (numMix.Value is decimal mv)
            arnndn += $":mix={(double)mv}";
        if (chkSpeechNorm.IsChecked == true)
            arnndn += ":speechnorm=1";
        filters.Add(arnndn);
        if (chkHighpass.IsChecked == true)
            filters.Add($"highpass=f={numHighpass.Value}");
        if (chkLowpass.IsChecked == true)
            filters.Add($"lowpass=f={numLowpass.Value}");
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
        var item = _queue.FirstOrDefault(q => q.IsChecked) ?? _queue.FirstOrDefault();
        if (item == null) return;
        var ffplay = Path.Combine(txtFfmpeg.Text, OperatingSystem.IsWindows() ? "ffplay.exe" : "ffplay");
        if (!File.Exists(ffplay))
        {
            tslStatus.Text = "ffplay not found";
            return;
        }
        var filter = BuildFilter();
        try
        {
            var psi = new ProcessStartInfo(ffplay) { UseShellExecute = false };
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(item.Input);
            psi.ArgumentList.Add("-af");
            psi.ArgumentList.Add(filter);
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            tslStatus.Text = ex.Message;
        }
    }

    async void BtnStart_Click(object? sender, RoutedEventArgs e)
    {
        SaveSettingsFromUi();
        if (_queue.Count == 0) return;
        btnStart.IsEnabled = false;
        btnCancel.IsEnabled = true;
        _cts = new CancellationTokenSource();
        tslStatus.Text = "Processing...";
        var ffmpeg = Path.Combine(txtFfmpeg.Text, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        foreach (var item in _queue)
        {
            if (_cts.IsCancellationRequested)
            {
                item.Status = "Canceled";
                break;
            }
            item.Status = "Processing";
            Directory.CreateDirectory(Path.GetDirectoryName(item.Output)!);
            var args = BuildFfmpegArgs(item.Input, item.Output);
            try
            {
                var psi = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, RedirectStandardError = true };
                foreach (var a in args)
                    psi.ArgumentList.Add(a);
                var proc = Process.Start(psi);
                if (proc != null)
                    await proc.WaitForExitAsync(_cts.Token);
                item.Status = proc?.ExitCode == 0 ? "Done" : "Error";
            }
            catch (OperationCanceledException)
            {
                item.Status = "Canceled";
                break;
            }
            catch (Exception ex)
            {
                item.Status = "Error";
                tslStatus.Text = ex.Message;
                break;
            }
        }
        tslStatus.Text = Localizer.Tr("Ready");
        btnStart.IsEnabled = true;
        btnCancel.IsEnabled = false;
        _cts = null;
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
        var win = new ReadmeWindow();
        await win.ShowDialog(this);
        if (win.DontShowAgain)
        {
            _settings.ShowReadme = false;
            _settings.Save(_settingsPath);
        }
    }
}
