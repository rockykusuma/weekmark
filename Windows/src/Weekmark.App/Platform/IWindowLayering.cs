using Avalonia.Controls;
using Weekmark.Core;

namespace Weekmark.App.Platform;

/// <summary>Puts the widget on the desktop or above windows. <see cref="Apply"/> is idempotent and re-applied on every clock tick.</summary>
public interface IWindowLayering
{
    void Apply(Window w, WindowLayer layer);
    void HideFromTaskSwitcher(Window w);

    static IWindowLayering ForCurrentPlatform() =>
        OperatingSystem.IsWindows() ? new WindowsLayering() : new FallbackLayering();
}

/// <summary>Non-Windows (and Win32 failure) fallback: "on desktop" is just a normal, non-topmost window.</summary>
public sealed class FallbackLayering : IWindowLayering
{
    public void Apply(Window w, WindowLayer layer) => w.Topmost = layer == WindowLayer.Floating;
    public void HideFromTaskSwitcher(Window w) { } // ShowInTaskbar=false is all Avalonia offers here
}
