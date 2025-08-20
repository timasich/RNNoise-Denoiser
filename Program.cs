using Avalonia;
using System;

namespace RNNoise_Denoiser;

class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static Avalonia.AppBuilder BuildAvaloniaApp()
        => Avalonia.AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
