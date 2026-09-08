using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FxShell.Views;

public partial class AboutPage : UserControl
{
    private const string GitHubUrl = "https://github.com/xiaochengzjc/CxShell";
    private const string KoFiUrl = "https://ko-fi.com/xiaochengzjc";

    public AboutPage()
    {
        InitializeComponent();
    }

    private void OnGitHubClick(object? sender, RoutedEventArgs e)
    {
        OpenExternalLink(GitHubUrl);
    }

    private void OnKoFiClick(object? sender, RoutedEventArgs e)
    {
        OpenExternalLink(KoFiUrl);
    }

    private static void OpenExternalLink(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // External link launching is best-effort on desktop platforms.
        }
    }
}
