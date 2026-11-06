using System.Globalization;
using Avalonia;
using Weekmark.Core;
using Velopack;

namespace Weekmark.App;

/// <summary>Command line: <c>--demo-data</c>, <c>--screenshot &lt;path&gt;</c> (implies demo data), <c>--exit-after &lt;seconds&gt;</c>
/// (after the screenshot when there is one; prints idle memory, then quits),
/// <c>--autostart</c>, <c>--settings [tab]</c> (opens Settings, e.g. on <c>planning</c>), <c>--onboarding</c> (shows the welcome window), <c>--year</c> (opens Year at a Glance), <c>--find [query]</c> (opens the Quick Finder, optionally with a query).
/// With <c>--screenshot</c>, the Quick Finder, year, settings or welcome window is captured instead of the widget when one is open.</summary>
public sealed record Options(bool DemoData, string? Screenshot, double? ExitAfter, bool Autostart, string? Settings = null, bool Onboarding = false, bool Year = false, string? Find = null)
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
            args.Contains("--onboarding"), args.Contains("--year"),
            args.Contains("--find") ? Value("--find") is { } q && !q.StartsWith("--") ? q : "" : null);
    }
}

static class Program
{
    public static Options Options { get; private set; } = Options.Parse([]);
    public static SingleInstance? Instance { get; private set; }
    public static (AppSettings Settings, SettingsStore? Store) Loaded { get; private set; } = (new(), null);
    /// <summary>Glass (Mica/Acrylic) needs the GPU renderer; every other style draws in software, about 30 MB less idle
    /// memory. Chosen at start, so switching to Glass takes effect after a restart.</summary>
    internal static bool SoftwareRendering { get; set; }
    // A screenshot never shows glass (no desktop behind a PNG), so it draws in software too.
    internal static bool SoftwareFor(AppSettings s, Options o) => !(s.Background == BackgroundStyle.Glass && o.Screenshot is null);

    [STAThread]
    public static int Main(string[] args)
    {
        CrashLog.Install();
        // Installer hooks; a no-op when not installed by Velopack. Uninstall (Windows only) drops the start-with-Windows value.
        var velopack = VelopackApp.Build();
        if (OperatingSystem.IsWindows())
            velopack.OnBeforeUninstallFastCallback(_ => { try { Platform.Startup.Apply(false, ""); } catch { /* never block uninstall */ } });
        // A second launch must not auto-apply a ready update: Update.exe would force-kill the running copy (ghost tray icon).
        // The mutex is only probed, never owned; it exists while another Weekmark runs.
        velopack.SetAutoApplyOnStartup(!Mutex.TryOpenExisting($@"Local\Weekmark-{Environment.UserName}", out var running));
        running?.Dispose();
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
        Loaded = AppState.Load(Options, SettingsStore.DefaultPath);
        SoftwareRendering = SoftwareFor(Loaded.Settings, Options);
        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnExplicitShutdown);
        }
        finally { Instance?.Dispose(); }
    }

    // Also used by the Avalonia designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var b = AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
        return SoftwareRendering ? b.With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] }) : b;
    }
}
