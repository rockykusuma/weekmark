using System.Globalization;
using Avalonia;
using Velopack;

namespace Weekmark.App;

/// <summary>Command line: <c>--demo-data</c>, <c>--screenshot &lt;path&gt;</c> (implies demo data), <c>--exit-after &lt;seconds&gt;</c>,
/// <c>--autostart</c>, <c>--settings [tab]</c> (opens Settings, e.g. on <c>planning</c>), <c>--onboarding</c> (shows the welcome window).
/// With <c>--screenshot</c>, the settings or welcome window is captured instead of the widget when one is open.</summary>
public sealed record Options(bool DemoData, string? Screenshot, double? ExitAfter, bool Autostart, string? Settings = null, bool Onboarding = false)
{
    public static Options Parse(string[] args)
    {
        string? Value(string flag) => Array.IndexOf(args, flag) is var i and >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        var screenshot = Value("--screenshot");
        // Anything captured automatically uses sample data, never the user's settings.
        return new(args.Contains("--demo-data") || screenshot is not null, screenshot,
            double.TryParse(Value("--exit-after"), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? s : null,
            args.Contains("--autostart"),
            args.Contains("--settings") ? Value("--settings") is { } tab && !tab.StartsWith("--") ? tab : "general" : null,
            args.Contains("--onboarding"));
    }
}

static class Program
{
    public static Options Options { get; private set; } = Options.Parse([]);
    public static SingleInstance? Instance { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        CrashLog.Install();
        // Installer hooks; a no-op when not installed by Velopack. Uninstall (Windows only) drops the start-with-Windows value.
        var velopack = VelopackApp.Build();
        if (OperatingSystem.IsWindows())
            velopack.OnBeforeUninstallFastCallback(_ => { try { Platform.Startup.Apply(false, ""); } catch { /* never block uninstall */ } });
        velopack.Run();
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
