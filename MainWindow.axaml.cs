using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.ObjectModel;
using System.IO;

namespace RNNoise_Denoiser;

public partial class MainWindow : Window
{
    readonly AppSettings _settings;
    readonly string _settingsPath;
    readonly ObservableCollection<QueueItem> _queue = new();

    public MainWindow()
    {
        InitializeComponent();

        foreach (var l in Localizer.Langs)
            cboLang.Items.Add(l);
        cboLang.SelectedIndex = 0;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "RNNoiseDenoiser");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
        _settings = AppSettings.Load(_settingsPath);
        LoadSettingsToUi();

        cboLang.SelectionChanged += (_, __) =>
        {
            if (cboLang.SelectedItem is LangItem li)
                Localizer.Set(li.Code);
        };

        chkHighpass.Checked += (_, __) => numHighpass.IsEnabled = true;
        chkHighpass.Unchecked += (_, __) => numHighpass.IsEnabled = false;
        chkLowpass.Checked += (_, __) => numLowpass.IsEnabled = true;
        chkLowpass.Unchecked += (_, __) => numLowpass.IsEnabled = false;

        btnFfmpegBrowse.Click += BtnFfmpegBrowse_Click;
        btnModelBrowse.Click += BtnModelBrowse_Click;
        btnOutputBrowse.Click += BtnOutputBrowse_Click;

        dgQueue.ItemsSource = _queue;
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

        cboAudioCodec.Items.Add("aac");
        cboAudioCodec.Items.Add("libmp3lame");
        cboAudioCodec.Items.Add("pcm_s16le");
        cboAudioCodec.SelectedItem = _settings.AudioCodec;

        foreach (var br in new[] { "128k", "160k", "192k", "256k", "320k" })
            cboBitrate.Items.Add(br);
        cboBitrate.SelectedItem = _settings.AudioBitrate;

        numMix.Value = (decimal)_settings.Profile.Mix;
        chkHighpass.IsChecked = _settings.Profile.HighpassHz.HasValue;
        numHighpass.Value = _settings.Profile.HighpassHz ?? 80;
        numHighpass.IsEnabled = chkHighpass.IsChecked == true;
        chkLowpass.IsChecked = _settings.Profile.LowpassHz.HasValue;
        numLowpass.Value = _settings.Profile.LowpassHz ?? 12000;
        numLowpass.IsEnabled = chkLowpass.IsChecked == true;
        chkSpeechNorm.IsChecked = _settings.Profile.SpeechNorm;
        chkCopyVideo.IsChecked = _settings.CopyVideo;
    }

    void SaveSettingsFromUi()
    {
        _settings.FfmpegBinPath = txtFfmpeg.Text;
        _settings.ModelPath = txtModel.Text;
        _settings.OutputFolder = txtOutput.Text;
        _settings.AudioCodec = (string?)cboAudioCodec.SelectedItem ?? "aac";
        _settings.AudioBitrate = (string?)cboBitrate.SelectedItem ?? "192k";
        _settings.Profile.Mix = (double)(numMix.Value ?? 0.85m);
        _settings.Profile.HighpassHz = chkHighpass.IsChecked == true ? (int?)numHighpass.Value : null;
        _settings.Profile.LowpassHz = chkLowpass.IsChecked == true ? (int?)numLowpass.Value : null;
        _settings.Profile.SpeechNorm = chkSpeechNorm.IsChecked == true;
        _settings.CopyVideo = chkCopyVideo.IsChecked == true;
        _settings.Save(_settingsPath);
    }

    protected override void OnClosed(EventArgs e)
    {
        SaveSettingsFromUi();
        base.OnClosed(e);
    }
}
