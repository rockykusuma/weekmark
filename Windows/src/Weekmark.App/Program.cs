using System.Globalization;
using Avalonia;
using Velopack;

namespace Weekmark.App;

/// <summary>Command line: <c>--demo-data</c>, <c>--screenshot &lt;path&gt;</c> (implies demo data), <c>--exit-after &lt;seconds&gt;</c>, <c>--autostart</c>.</summary>
public sealed record Options(bool DemoData, string? Screenshot, double? ExitAfter, bool Autostart)
{
    public static Options Parse(string[] args)
    {
        string? Value(string flag) => Array.IndexOf(args, flag) is var i and >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        var screenshot = Value("--screenshot");
        // Anything captured automatically uses sample data, never the user's settings.
        return new(args.Contains("--demo-data") || screenshot is not null, screenshot,
            double.TryParse(Value("--exit-after"), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? s : null,
            args.Contains("--autostart"));
    }
}

static class Program
{
    public static Options Options { get; private set; } = Options.Parse([]);
    public static SingleInstance? Instance { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        VelopackApp.Build().Run(); // installer hooks; a no-op when not installed by Velopack
        Options = Options.Parse(args);

        // Demo runs touch no settings, so they may run beside the real app (screenshots, development).
        if (!Options.DemoData)
        {
            var name = $"Weekmark-{Environment.UserName}";
            Instance = SingleInstance.TryAcquire(name);
            if (Instance is null)
            {
                SingleInstance.Signal(name);
                return 0;
            }
        }
        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnExplicitShutdown);
        }
        finally { Instance?.Dispose(); }
    }

    // Also used by the Avalonia designer.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
