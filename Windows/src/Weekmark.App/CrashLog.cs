using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Threading;

namespace Weekmark.App;

/// <summary>
/// Appends unhandled exceptions to <c>%LOCALAPPDATA%\Weekmark\crash.log</c>: time, app and OS version, and each
/// exception's type and stack trace. Never messages (they can quote settings) and never settings content.
/// The file stays under about 1 MB: when it would grow past that, the oldest half is dropped.
/// </summary>
public static class CrashLog
{
    internal const int Cap = 1 << 20;

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Weekmark", "crash.log");

    static Exception? last;

    /// <summary>At startup, before Avalonia. Logging only: crashes still crash, unobserved task exceptions stay ignored.</summary>
    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Write(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => Write(e.Exception);
    }

    /// <summary>Once Avalonia runs. An exception the UI thread doesn't handle reaches the AppDomain too: logged once.</summary>
    public static void InstallUIThread() => Dispatcher.UIThread.UnhandledException += (_, e) => Write(e.Exception);

    /// <summary>Never throws.</summary>
    internal static void Write(Exception? e, string? path = null)
    {
        try
        {
            if (e is not null && ReferenceEquals(e, Interlocked.Exchange(ref last, e))) return;
            path ??= DefaultPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";
            var entry = new StringBuilder($"{DateTimeOffset.Now:O} Weekmark {version} on {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})\n");
            Describe(e, entry, 0);
            entry.Append('\n');

            var text = entry.ToString();
            if (File.Exists(path) && new FileInfo(path).Length + Encoding.UTF8.GetByteCount(text) > Cap)
            {
                var old = File.ReadAllText(path);
                var nl = old.IndexOf('\n', old.Length / 2); // ponytail: may cut an old entry mid-way; fine for a log
                File.WriteAllText(path, old[(nl < 0 ? old.Length / 2 : nl + 1)..] + text);
            }
            else File.AppendAllText(path, text);
        }
        catch { /* a crash logger must never crash */ }
    }

    static void Describe(Exception? e, StringBuilder sb, int depth)
    {
        if (e is null || depth > 8) return;
        sb.Append(depth == 0 ? "" : "--- inner: ").Append(e.GetType().FullName).Append('\n');
        if (e.StackTrace is { } st) sb.Append(st).Append('\n');
        if (e is AggregateException a) foreach (var i in a.InnerExceptions) Describe(i, sb, depth + 1);
        else Describe(e.InnerException, sb, depth + 1);
    }
}
