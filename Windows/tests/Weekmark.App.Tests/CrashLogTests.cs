namespace Weekmark.App.Tests;

public sealed class CrashLogTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("wm-crash-").FullName;
    string LogPath => Path.Combine(dir, "Weekmark", "crash.log");

    public void Dispose() => Directory.Delete(dir, recursive: true);

    static Exception Thrown(Func<Exception> make)
    {
        try { throw make(); }
        catch (Exception e) { return e; }
    }

    [Fact]
    public void DefaultPathIsInLocalAppData() => Assert.Equal(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Weekmark", "crash.log"),
        CrashLog.DefaultPath);

    [Fact]
    public void WritesTypeAndStackButNoMessage()
    {
        var e = Thrown(() => new InvalidOperationException("""settings {"milestones":[{"name":"Secret launch"}]}""",
            new FormatException("inner Secret")));
        CrashLog.Write(e, LogPath);

        var log = File.ReadAllText(LogPath);
        Assert.Contains("Weekmark ", log);
        Assert.Contains(System.Runtime.InteropServices.RuntimeInformation.OSDescription, log);
        Assert.Contains("System.InvalidOperationException", log);
        Assert.Contains("--- inner: System.FormatException", log);
        Assert.Contains(nameof(Thrown), log); // the stack trace
        Assert.DoesNotContain("Secret", log);
        Assert.DoesNotContain("milestones", log);
    }

    [Fact]
    public void StaysUnderTheCapByDroppingTheOldestHalf()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        File.WriteAllText(LogPath, string.Concat(Enumerable.Range(0, 40000).Select(i => $"old line {i:D6}\n")) + new string('x', CrashLog.Cap / 2));
        CrashLog.Write(Thrown(() => new TimeoutException()), LogPath);

        var log = File.ReadAllText(LogPath);
        Assert.True(new FileInfo(LogPath).Length <= CrashLog.Cap, $"{new FileInfo(LogPath).Length} bytes");
        Assert.DoesNotContain("old line 000000", log);
        Assert.EndsWith("\n", log);
        Assert.Contains("System.TimeoutException", log);

        File.WriteAllText(LogPath, new string('x', CrashLog.Cap)); // no line breaks at all
        CrashLog.Write(Thrown(() => new TimeoutException()), LogPath);
        Assert.True(new FileInfo(LogPath).Length <= CrashLog.Cap, $"{new FileInfo(LogPath).Length} bytes");
    }

    [Fact]
    public void NeverThrows()
    {
        File.WriteAllText(Path.Combine(dir, "file"), "");
        CrashLog.Write(new Exception(), Path.Combine(dir, "file", "crash.log")); // its folder is a file
        CrashLog.Write(null, LogPath);
    }
}
