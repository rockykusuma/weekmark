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
/// Recovery: <see cref="Apply"/> is re-run on every 30 s tick, restoring the window if anything minimised it and
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

    public void Apply(Window w, WindowLayer layer)
    {
        if (Handle(w) is not { } h) { fallback.Apply(w, layer); return; }
        try
        {
            if (IsIconic(h)) ShowWindow(h, SW_SHOWNOACTIVATE); // minimised by Show desktop / Win+M: bring it back
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
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or MarshalDirectiveException)
        {
            fallback.Apply(w, layer);
        }
    }

    public void HideFromTaskSwitcher(Window w)
    {
        static uint Fix(uint ex) => (ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE) & ~WS_EX_APPWINDOW;
        try
        {
            // The callback keeps the styles whenever Avalonia recomputes them; the direct call applies them now.
            Win32Properties.AddWindowStylesCallback(w, (style, ex) => (style, Fix(ex)));
            if (Handle(w) is { } h) SetWindowLongPtrW(h, GWL_EXSTYLE, (nint)Fix((uint)GetWindowLongPtrW(h, GWL_EXSTYLE)));
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { }
    }

    /// <summary>Windows 11 rounds the corners of the glass widget (no effect on Windows 10).</summary>
    public static void RoundCorners(Window w)
    {
        try
        {
            if (Handle(w) is { } h) DwmSetWindowAttribute(h, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND, sizeof(int));
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { }
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
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(nint hWnd, int cmd);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint hWnd, int attribute, in int value, int size);
}
