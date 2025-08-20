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
        var arnndn = $"arnndn=m='{txtModel.Text}'";
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

    string BuildFfmpegArgs(string input, string output)
    {
        var filter = BuildFilter();
        var args = $"-i \"{input}\" -af \"{filter}\" -c:a {(string?)cboAudioCodec.SelectedItem ?? "aac"} -b:a {(string?)cboBitrate.SelectedItem ?? "192k"}";
        if (chkCopyVideo.IsChecked == true)
            args += " -c:v copy";
        else
            args += " -vn";
        args += $" \"{output}\" -y";
        return args;
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

    void BtnCheckEnv_Click(object? sender, RoutedEventArgs e)
    {
        var ffmpeg = Path.Combine(txtFfmpeg.Text, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        var ffprobe = Path.Combine(txtFfmpeg.Text, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");
        if (!File.Exists(ffmpeg) || !File.Exists(ffprobe))
        {
            tslStatus.Text = Localizer.Tr("ffmpeg.exe/ffprobe.exe not found. Specify path to bin folder.");
            return;
        }
        if (!File.Exists(txtModel.Text))
        {
            tslStatus.Text = Localizer.Tr(".rnnn model file not found.");
            return;
        }
        tslStatus.Text = "Environment OK";
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
            Process.Start(new ProcessStartInfo(ffplay, $"-i \"{item.Input}\" -af \"{filter}\"") { UseShellExecute = true });
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
                var psi = new ProcessStartInfo(ffmpeg, args) { UseShellExecute = false, RedirectStandardError = true };
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
