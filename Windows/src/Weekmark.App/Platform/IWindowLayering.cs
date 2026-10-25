using Avalonia.Controls;
using Weekmark.Core;

namespace Weekmark.App.Platform;

/// <summary>Puts the widget on the desktop or above windows. <see cref="Apply"/> is idempotent and re-applied on every clock tick.</summary>
public interface IWindowLayering
{
    void Apply(Window w, WindowLayer layer);
    void HideFromTaskSwitcher(Window w);
    /// <summary>
    /// True while the search field is in use: the widget may take keyboard focus. False gives it back, and with
    /// <paramref name="handBack"/> also returns the foreground to the window that had it before.
    /// </summary>
    void AllowActivation(Window w, bool allow, bool handBack = true);

    static IWindowLayering ForCurrentPlatform() =>
        OperatingSystem.IsWindows() ? new WindowsLayering() : new FallbackLayering();
}

/// <summary>Non-Windows (and Win32 failure) fallback: "on desktop" is just a normal, non-topmost window.</summary>
public sealed class FallbackLayering : IWindowLayering
{
    public void Apply(Window w, WindowLayer layer) => w.Topmost = layer == WindowLayer.Floating;
    public void HideFromTaskSwitcher(Window w) { } // ShowInTaskbar=false is all Avalonia offers here
    public void AllowActivation(Window w, bool allow, bool handBack = true) { } // nothing blocks activation here
}
