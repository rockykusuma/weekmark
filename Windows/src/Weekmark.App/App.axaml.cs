using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Weekmark.App.Platform;
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
            Startup.Apply(state.Settings.StartWithWindows, exe);
        ShowWidget();
        tray = new TrayController(state, Widget!);
        tray.QuitRequested += () => Quit();
        // tray.SettingsRequested: the settings window (Task 11) subscribes.
        // Launched by hand, the widget may be hidden behind windows: show it once. A login launch shows nothing extra.
        if (!o.Autostart) Dispatcher.UIThread.Post(() => Widget?.BringForward());

        if (Program.Instance is { } instance)
            instance.ActivationRequested += () => Dispatcher.UIThread.Post(() => Widget?.BringForward());
        if (o.Screenshot is { } path)
            Dispatcher.UIThread.Post(() => // Background priority: runs after the shown widget's first layout pass
            {
                Screenshot.Save(Widget!.View, path);
                Console.WriteLine($"screenshot={Path.GetFullPath(path)}");
            }, DispatcherPriority.Background);
        if (o.ExitAfter is { } seconds)
            DispatcherTimer.RunOnce(() =>
            {
                Console.WriteLine($"working_set_mb={Math.Round(Environment.WorkingSet / 1048576.0)}");
                Quit();
            }, TimeSpan.FromSeconds(seconds));
    }

    void ShowWidget()
    {
        Widget = new WidgetWindow(state!, layering, glassAllowed: Program.Options.Screenshot is null);
        // The widget never closes by itself; if Windows destroys it (Explorer restart takes its owner along), recreate it.
        Widget.Closed += (_, _) => { if (!quitting) Dispatcher.UIThread.Post(ShowWidget); };
        Widget.Show();
        if (tray is not null) tray.Widget = Widget;
    }

    public void Quit(int code = 0)
    {
        quitting = true;
        tray?.Dispose(); // otherwise Windows keeps a ghost icon until the mouse passes over it
        state?.Dispose();
        desktop?.Shutdown(code);
    }
}
