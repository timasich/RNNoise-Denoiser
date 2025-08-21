using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace RNNoise_Denoiser;

public sealed class DenoiseProfile
{
    public double Mix { get; set; } = 0.85;
    public int? HighpassHz { get; set; }
    public int? LowpassHz { get; set; }
    public bool SpeechNorm { get; set; }
}

public sealed class AppSettings
{
    public string FfmpegBinPath { get; set; } = "";
    public string ModelPath { get; set; } = "";
    public string OutputFolder { get; set; } = GetDefaultOutputFolder();
    public DenoiseProfile Profile { get; set; } = new();
    public string AudioCodec { get; set; } = "aac";
    public string AudioBitrate { get; set; } = "192k";
    public bool CopyVideo { get; set; } = true;
    public string Language { get; set; } = "";
    public bool ShowReadme { get; set; } = true;
    public Dictionary<string, DenoiseProfile> CustomPresets { get; set; } = new();

    public static string GetDefaultOutputFolder()
    {
        if (OperatingSystem.IsWindows())
            return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (OperatingSystem.IsLinux())
            return Environment.GetEnvironmentVariable("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (OperatingSystem.IsMacOS())
            return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    }

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
    public bool IsChecked { get; set; }
    public string Input { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Progress { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string ErrorDetails { get; set; } = string.Empty;
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
