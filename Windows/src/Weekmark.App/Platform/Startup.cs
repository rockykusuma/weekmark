using Microsoft.Win32;

namespace Weekmark.App.Platform;

/// <summary>"Start with Windows": the HKCU Run value <c>Weekmark</c> = <c>"&lt;exe&gt;" --autostart</c>. No-op off Windows.</summary>
public static class Startup
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", ValueName = "Weekmark";

    /// <summary>Adds or removes the Run value. Called at every launch so the value follows the exe if it moved.
    /// Registry failures (policy, permissions) leave things as they were: starting the app matters more.</summary>
    public static void Apply(bool enabled, string exePath)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            var command = $"\"{exePath}\" --autostart";
            if (!enabled) key.DeleteValue(ValueName, throwOnMissingValue: false);
            else if (key.GetValue(ValueName) as string != command) key.SetValue(ValueName, command);
        }
        catch (Exception e) when (e is System.Security.SecurityException or IOException or UnauthorizedAccessException) { }
    }

    public static bool IsRegistered()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
        catch (Exception e) when (e is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
