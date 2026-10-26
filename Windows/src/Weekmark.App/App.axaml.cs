using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Weekmark.App.Finder;
using Weekmark.App.Onboarding;
using Weekmark.App.Platform;
using Weekmark.App.Settings;
using Weekmark.App.Tray;
using Weekmark.App.Updates;
using Weekmark.App.Widget;
using Weekmark.App.Year;
using Weekmark.Core;

namespace Weekmark.App;

public partial class App : Application
{
    IClassicDesktopStyleApplicationLifetime? desktop;
    AppState? state;
    IWindowLayering layering = new FallbackLayering();
    TrayController? tray;
    Updater? updater;
    SettingsWindow? settingsWindow;
    YearWindow? yearWindow;
    QuickFinderWindow? finder;
    WelcomeWindow? welcomeWindow;
    string? startupExe;
    bool quitting;

    public WidgetWindow? Widget { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime d) return; // headless tests drive views directly
        desktop = d;
        CrashLog.InstallUIThread();
        desktop.ShutdownRequested += (_, _) => quitting = true;

        var o = Program.Options;
        state = AppState.FromOptions(o, SettingsStore.DefaultPath);
        state.Start();
        layering = IWindowLayering.ForCurrentPlatform();
        startupExe = StartupExe(o, Environment.ProcessPath); // the settings and welcome windows re-apply it when the toggle changes
        if (startupExe is not null) Startup.Apply(state.Settings.StartWithWindows, startupExe);
        updater = new Updater(state, new VelopackEngine(), autoChecks: !o.DemoData);
        CreateWidget();
        tray = new TrayController(state, Widget!) { Updater = updater };
        tray.RestartRequested += RestartToUpdate;
        tray.CheckForUpdatesRequested += () =>
        {
            OpenSettings("general");
            settingsWindow!.ShowUpdates();
            _ = updater.CheckNow();
        };
        tray.QuitRequested += () => Quit();
        tray.SettingsRequested += () => OpenSettings();
        tray.YearRequested += OpenYear;
        tray.FinderRequested += () => Finder().ShowFinder();
        tray.AddMilestoneRequested += AddMilestone;
        if (o.Onboarding || (!o.DemoData && !state.Settings.OnboardingDone))
        {
            OpenWelcome();
        }
        ShowAfter(welcomeWindow, Widget!, () => quitting); // first run: the widget appears after the welcome
        // Launched by hand, the widget may be hidden behind windows: show it once. A login launch shows nothing extra,
        // and neither does a first run, where the welcome window has the focus.
        if (welcomeWindow is null && !o.Autostart) Dispatcher.UIThread.Post(() => Widget?.BringForward());
        if (o.Settings is { } tab) OpenSettings(tab);
        if (o.Year) OpenYear();
        if (o.Find is { } query) Dispatcher.UIThread.Post(() => Finder().ShowFinder(query));

        if (Program.Instance is { } instance)
            instance.ActivationRequested += () => Dispatcher.UIThread.Post(() => Widget?.BringForward());
        if (o.Screenshot is { } path)
            Dispatcher.UIThread.Post(() => // Background priority: runs after the shown widget's first layout pass
            {
                Screenshot.Save((Control?)(finder is { IsVisible: true } ? finder : null) ?? (Control?)yearWindow ?? (Control?)settingsWindow ?? (Control?)welcomeWindow ?? Widget!.View, path);
                Console.WriteLine($"screenshot={Path.GetFullPath(path)}");
                ExitLater(o.ExitAfter);
            }, DispatcherPriority.Background);
        else ExitLater(o.ExitAfter);
    }

    /// <summary><c>--exit-after</c>: counted from the screenshot when there is one, so the figure is the idle app's.</summary>
    void ExitLater(double? seconds)
    {
        if (seconds is { } s) DispatcherTimer.RunOnce(() => { ReportMemory(); Quit(); }, TimeSpan.FromSeconds(s));
    }

    /// <summary>Idle memory after a full collection: the working set (the spec's target) and private bytes.</summary>
    static void ReportMemory()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        using var p = System.Diagnostics.Process.GetCurrentProcess();
        static double Mb(long bytes) => Math.Round(bytes / 1048576.0);
        Console.WriteLine($"working_set_mb={Mb(p.WorkingSet64)} private_mb={Mb(p.PrivateMemorySize64)}");
    }

    /// <summary>
    /// The exe to register to start with Windows, or null to leave the entry alone: demo runs (like their settings),
    /// <c>dotnet Weekmark.dll</c> (the process is dotnet itself) and Debug builds (<c>dotnet run</c> from source would
    /// register bin\Debug\…\Weekmark.exe). Release builds, installed or from <c>dotnet publish</c>, register.
    /// </summary>
    internal static string? StartupExe(Options o, string? processPath)
    {
#if DEBUG
        return null;
#else
        return !o.DemoData && processPath is { } exe && Path.GetFileNameWithoutExtension(exe) == "Weekmark" ? exe : null;
#endif
    }

    /// <summary>Shows the widget when the welcome window closes (Done or its close button), or now when there is none.</summary>
    internal static void ShowAfter(Window? welcome, Window widget, Func<bool> quitting)
    {
        if (welcome is null) widget.Show();
        else welcome.Closed += (_, _) => { if (!quitting()) widget.Show(); };
    }

    /// <summary>One settings window at a time: a second request brings the open one forward.</summary>
    void OpenSettings(string? tab = null)
    {
        if (settingsWindow is null)
        {
            var vm = new SettingsViewModel(state!, startupExe, updater);
            vm.RestartRequested += RestartToUpdate;
            vm.WelcomeRequested += OpenWelcome;
            settingsWindow = new SettingsWindow(vm, tab);
            settingsWindow.Closed += (_, _) => settingsWindow = null;
            settingsWindow.Show();
        }
        settingsWindow.Activate();
    }

    void AddMilestone(WeekRef w)
    {
        OpenSettings("milestones");
        settingsWindow!.AddMilestone(w);
    }

    /// <summary>One welcome window at a time, like the year window.</summary>
    void OpenWelcome()
    {
        if (welcomeWindow is null)
        {
            welcomeWindow = new WelcomeWindow(new WelcomeViewModel(state!, WelcomeViewModel.CurrentRegion(), startupExe));
            welcomeWindow.Closed += (_, _) => welcomeWindow = null;
            welcomeWindow.Show();
        }
        if (welcomeWindow.WindowState == WindowState.Minimized) welcomeWindow.WindowState = WindowState.Normal;
        welcomeWindow.Activate();
    }

    /// <summary>One Year at a Glance window at a time: a second request brings the open one forward.</summary>
    void OpenYear()
    {
        if (yearWindow is null)
        {
            var screens = Widget!.ScreenInfos();
            yearWindow = new YearWindow(state!, screens.FirstOrDefault(x => x.Id == Widget.CurrentMonitor()));
            yearWindow.Closed += (_, _) => yearWindow = null;
            yearWindow.Show();
        }
        if (yearWindow.WindowState == WindowState.Minimized) yearWindow.WindowState = WindowState.Normal;
        yearWindow.Activate();
    }

    /// <summary>One finder for the whole run; closing it only hides it.</summary>
    QuickFinderWindow Finder()
    {
        if (finder is null)
        {
            finder = new QuickFinderWindow(state!);
            finder.ShowInWidgetRequested += r => Widget!.ShowWeek(r);
        }
        return finder;
    }

    void CreateWidget()
    {
        Widget = new WidgetWindow(state!, layering, glassAllowed: Program.Options.Screenshot is null);
        // The widget never closes by itself; if Windows destroys it (Explorer restart takes its owner along), recreate it.
        Widget.Closed += (_, _) => { if (!quitting) Dispatcher.UIThread.Post(ShowWidget); };
        Widget.YearRequested += OpenYear;
        Widget.HotKeyPressed += () => Finder().Toggle();
        if (tray is not null) tray.Widget = Widget;
    }

    void ShowWidget()
    {
        CreateWidget();
        Widget!.Show();
    }

    /// <summary>A successful restart exits the process without returning, so the tray icon goes first or Windows keeps a ghost.
    /// If the restart fails, the icon comes back.</summary>
    void RestartToUpdate()
    {
        if (tray is null || updater is null) return;
        tray.Icon.IsVisible = false;
        updater.RestartToUpdate();
        tray.Icon.IsVisible = true;
    }

    public void Quit(int code = 0)
    {
        quitting = true;
        updater?.Dispose();
        tray?.Dispose(); // otherwise Windows keeps a ghost icon until the mouse passes over it
        state?.Dispose();
        desktop?.Shutdown(code);
    }
}
