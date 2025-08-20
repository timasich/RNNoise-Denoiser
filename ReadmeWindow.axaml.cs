using Avalonia.Controls;
using Avalonia.Interactivity;
using System.IO;

namespace RNNoise_Denoiser;

public partial class ReadmeWindow : Window
{
    public bool DontShowAgain => chkDontShow.IsChecked == true;

    public ReadmeWindow()
    {
        InitializeComponent();
        var path = Path.Combine(AppContext.BaseDirectory, "README.md");
        if (File.Exists(path))
            txtReadme.Text = File.ReadAllText(path);
        btnOk.Click += (_, __) => Close();
    }
}
