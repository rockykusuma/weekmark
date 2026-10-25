using Microsoft.Win32;

namespace Weekmark.App.Platform;

public static class TaskbarTheme
{
    /// <summary>True when the taskbar uses the light theme (HKCU …\Personalize\SystemUsesLightTheme = 1).
    /// False when the value is missing (Windows 10 before 1903 has a dark taskbar), unreadable, or off Windows.</summary>
    public static bool IsLight()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int v && v != 0;
        }
        catch (Exception e) when (e is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
