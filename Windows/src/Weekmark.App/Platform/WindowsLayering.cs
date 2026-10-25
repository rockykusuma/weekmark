using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;
using Weekmark.Core;

namespace Weekmark.App.Platform;

/// <summary>
/// Win32 layering. "On desktop" makes the desktop's Progman window the widget's <i>owner</i>
/// (GWLP_HWNDPARENT) and sends it to the bottom of the z-order: an owned window always stays directly above its
/// owner, and Progman sits at the very bottom, so the widget lives just above the wallpaper and below every app
/// window. Show desktop (Win+D) raises the desktop rather than minimising windows the desktop owns, so the widget
/// comes up together with it instead of disappearing. WS_EX_NOACTIVATE keeps clicks from raising it, and
/// WS_EX_TOOLWINDOW keeps it out of the taskbar and Alt+Tab.
/// Recovery: <see cref="Apply"/> is re-run on every 30 s tick, restoring the window if anything minimised or hid it and
/// re-attaching it to a new Progman after Explorer restarts. Every failure falls back to a plain non-topmost window.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsLayering : IWindowLayering
{
    const int GWL_EXSTYLE = -20, GWLP_HWNDPARENT = -8, SW_SHOWNOACTIVATE = 4;
    const uint WS_EX_TOOLWINDOW = 0x80, WS_EX_APPWINDOW = 0x40000, WS_EX_NOACTIVATE = 0x08000000;
    const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2;
    static readonly nint HWND_BOTTOM = 1;

    readonly FallbackLayering fallback = new();
    bool broken; // after any Win32 failure: plain fallback for the rest of the session, no retrying
    bool activatable; // the search field is in use: WS_EX_NOACTIVATE is off
    nint previous; // the foreground window before the field took focus

    /// <summary>Only called while the app wants the widget visible (an Avalonia-hidden widget is never re-shown here).</summary>
    public void Apply(Window w, WindowLayer layer)
    {
        if (broken || Handle(w) is not { } h) { fallback.Apply(w, layer); return; }
        try
        {
            // Minimised (Win+M) or hidden (Show desktop) behind Avalonia's back: bring it back without activating it.
            if (IsIconic(h) || !IsWindowVisible(h)) ShowWindow(h, SW_SHOWNOACTIVATE);
            if (layer == WindowLayer.Floating)
            {
                SetWindowLongPtrW(h, GWLP_HWNDPARENT, 0);
                w.Topmost = true;
                return;
            }
            w.Topmost = false;
            var desktop = FindWindowW("Progman", null);
            if (desktop == 0) { fallback.Apply(w, layer); return; }
            if (GetWindowLongPtrW(h, GWLP_HWNDPARENT) != desktop) SetWindowLongPtrW(h, GWLP_HWNDPARENT, desktop);
            SetWindowPos(h, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
        catch (Exception) // a layering failure must never take the app down
        {
            broken = true;
            fallback.Apply(w, layer);
        }
    }

    uint Fix(uint ex) => ((activatable ? ex & ~WS_EX_NOACTIVATE : ex | WS_EX_NOACTIVATE) | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;

    public void HideFromTaskSwitcher(Window w)
    {
        try
        {
            // The callback keeps the styles whenever Avalonia recomputes them; the direct call applies them now.
            Win32Properties.AddWindowStylesCallback(w, (style, ex) => (style, Fix(ex)));
            if (Handle(w) is { } h) SetWindowLongPtrW(h, GWL_EXSTYLE, (nint)Fix((uint)GetWindowLongPtrW(h, GWL_EXSTYLE)));
        }
        catch (Exception) { } // cosmetic: never worth a crash
    }

    /// <summary>
    /// Typing needs keyboard focus, which WS_EX_NOACTIVATE refuses: it is off while the field is in use. Giving focus
    /// back restores it and returns the foreground to the window that had it, unless the user already moved on.
    /// </summary>
    public void AllowActivation(Window w, bool allow)
    {
        activatable = allow; // first: a widget window re-created after a close gets the right style too
        if (broken || Handle(w) is not { } h) return; // broken: the widget may stay activatable, which is harmless
        try
        {
            if (allow) previous = GetForegroundWindow();
            SetWindowLongPtrW(h, GWL_EXSTYLE, (nint)Fix((uint)GetWindowLongPtrW(h, GWL_EXSTYLE)));
            if (allow) SetForegroundWindow(h); // the click just went to this process, so Windows lets it take the keys
            else
            {
                if (previous != 0 && previous != h && GetForegroundWindow() == h && IsWindow(previous)) SetForegroundWindow(previous);
                previous = 0;
            }
        }
        catch (Exception) { broken = true; } // a layering failure must never take the app down
    }

    /// <summary>Windows 11 rounds the corners of the glass widget (no effect on Windows 10).</summary>
    public static void RoundCorners(Window w)
    {
        try
        {
            if (Handle(w) is { } h) DwmSetWindowAttribute(h, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND, sizeof(int));
        }
        catch (Exception) { } // cosmetic: never worth a crash
    }

    static nint? Handle(Window w) => w.TryGetPlatformHandle()?.Handle is { } h && h != 0 ? h : null;

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint FindWindowW(string className, string? windowName);

    [LibraryImport("user32.dll")]
    private static partial nint GetWindowLongPtrW(nint hWnd, int index);

    [LibraryImport("user32.dll")]
    private static partial nint SetWindowLongPtrW(nint hWnd, int index, nint value);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint hWnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(nint hWnd);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindow(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(nint hWnd, int cmd);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint hWnd, int attribute, in int value, int size);
}
