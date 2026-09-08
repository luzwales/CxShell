using System;
using Avalonia;
using AtomUI;
using FxShell.Services;
using ReactiveUI.Avalonia;
using Velopack;

namespace FxShell;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build()
            .SetArgs(args)
            .Run();

        if (CommandLineHandoffService.TrySendToExistingInstance(args))
            return;

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UseReactiveUI(builder => { })
            .UsePlatformDetect()
            .With(new MacOSPlatformOptions
            {
                DisableSetProcessName = false
            })
            .WithAtomUIDefaultOptions()
            .LogToTrace();
    }
}
