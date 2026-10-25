using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Weekmark.App.Onboarding;
using Weekmark.App.Platform;
using Weekmark.App.Settings;
using Weekmark.App.Tray;
using Weekmark.App.Widget;
using Weekmark.Core;

namespace Weekmark.App;

public partial class App : Application
{
    IClassicDesktopStyleApplicationLifetime? desktop;
    AppState? state;
    IWindowLayering layering = new FallbackLayering();
    TrayController? tray;
    SettingsWindow? settingsWindow;
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
        desktop.ShutdownRequested += (_, _) => quitting = true;

        var o = Program.Options;
        state = AppState.FromOptions(o, SettingsStore.DefaultPath);
        state.Start();
        layering = IWindowLayering.ForCurrentPlatform();
        // Demo runs leave the user's start-with-Windows entry alone, like their settings. Under `dotnet Weekmark.dll`
        // the process is dotnet itself, which must not be registered.
        if (!o.DemoData && Environment.ProcessPath is { } exe && Path.GetFileNameWithoutExtension(exe) == "Weekmark")
        {
            startupExe = exe; // the settings and welcome windows re-apply it when the toggle changes
            Startup.Apply(state.Settings.StartWithWindows, exe);
        }
        CreateWidget();
        tray = new TrayController(state, Widget!);
        tray.QuitRequested += () => Quit();
        tray.SettingsRequested += () => OpenSettings();
        if (o.Onboarding || (!o.DemoData && !state.Settings.OnboardingDone))
        {
            welcomeWindow = new WelcomeWindow(new WelcomeViewModel(state, WelcomeViewModel.CurrentRegion(), startupExe));
            welcomeWindow.Closed += (_, _) => welcomeWindow = null;
            welcomeWindow.Show();
        }
        ShowAfter(welcomeWindow, Widget!, () => quitting); // first run: the widget appears after the welcome
        // Launched by hand, the widget may be hidden behind windows: show it once. A login launch shows nothing extra,
        // and neither does a first run, where the welcome window has the focus.
        if (welcomeWindow is null && !o.Autostart) Dispatcher.UIThread.Post(() => Widget?.BringForward());
        if (o.Settings is { } tab) OpenSettings(tab);

        if (Program.Instance is { } instance)
            instance.ActivationRequested += () => Dispatcher.UIThread.Post(() => Widget?.BringForward());
        if (o.Screenshot is { } path)
            Dispatcher.UIThread.Post(() => // Background priority: runs after the shown widget's first layout pass
            {
                Screenshot.Save((Control?)settingsWindow ?? (Control?)welcomeWindow ?? Widget!.View, path);
                Console.WriteLine($"screenshot={Path.GetFullPath(path)}");
            }, DispatcherPriority.Background);
        if (o.ExitAfter is { } seconds)
            DispatcherTimer.RunOnce(() =>
            {
                Console.WriteLine($"working_set_mb={Math.Round(Environment.WorkingSet / 1048576.0)}");
                Quit();
            }, TimeSpan.FromSeconds(seconds));
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
            settingsWindow = new SettingsWindow(new SettingsViewModel(state!, startupExe), tab);
            settingsWindow.Closed += (_, _) => settingsWindow = null;
            settingsWindow.Show();
        }
        settingsWindow.Activate();
    }

    void CreateWidget()
    {
        Widget = new WidgetWindow(state!, layering, glassAllowed: Program.Options.Screenshot is null);
        // The widget never closes by itself; if Windows destroys it (Explorer restart takes its owner along), recreate it.
        Widget.Closed += (_, _) => { if (!quitting) Dispatcher.UIThread.Post(ShowWidget); };
        if (tray is not null) tray.Widget = Widget;
    }

    void ShowWidget()
    {
        CreateWidget();
        Widget!.Show();
    }

    public void Quit(int code = 0)
    {
        quitting = true;
        tray?.Dispose(); // otherwise Windows keeps a ghost icon until the mouse passes over it
        state?.Dispose();
        desktop?.Shutdown(code);
    }
}
