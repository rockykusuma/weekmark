using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Weekmark.App.Updates;
using Weekmark.Core;

namespace Weekmark.App.Tests;

public sealed class UpdaterTests
{
    static readonly DateTime Now = new(2026, 10, 25, 14, 30, 0);

    internal sealed class FakeEngine : IUpdateEngine
    {
        public bool IsInstalled { get; set; } = true;
        public string? CurrentVersion { get; set; } = "0.3.0";
        public string? Found;
        public Exception? CheckError, DownloadError;
        public TaskCompletionSource? HoldCheck, HoldDownload;
        public int Checks, Downloads, Applies;

        public async Task<string?> CheckAsync()
        {
            Checks++;
            if (HoldCheck is { } h) await h.Task;
            if (CheckError is not null) throw CheckError;
            return Found;
        }

        public async Task DownloadAsync(Action<int> progress)
        {
            Downloads++;
            if (HoldDownload is { } h) await h.Task;
            if (DownloadError is not null) throw DownloadError;
        }

        public void ApplyAndRestart() => Applies++;
    }

    internal static AppState State(bool auto = true) =>
        new(new AppSettings { AutomaticUpdateChecks = auto }, null, () => Now);

    static async Task Until(Func<bool> done)
    {
        for (var i = 0; i < 300 && !done(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
        Assert.True(done());
    }

    [AvaloniaFact]
    public async Task UpToDate()
    {
        using var u = new Updater(State(), new FakeEngine(), autoChecks: false);
        Assert.Equal(UpdateStage.Idle, u.Stage);
        await u.CheckNow();
        Assert.Equal(UpdateStage.UpToDate, u.Stage);
        Assert.Equal("Weekmark 0.3.0 is the latest version", u.StatusText);
        Assert.Equal("Last checked: 14:30", u.LastCheckedText);
    }

    [AvaloniaFact]
    public void LastCheckedNever()
    {
        using var u = new Updater(State(), new FakeEngine(), autoChecks: false);
        Assert.Equal("Last checked: never", u.LastCheckedText);
    }

    [AvaloniaFact]
    public async Task UpdateDownloadsThenReady()
    {
        var e = new FakeEngine { Found = "0.3.1", HoldDownload = new() };
        using var u = new Updater(State(), e, autoChecks: false);
        var changes = 0;
        u.Changed += () => changes++;
        var check = u.CheckNow();
        Assert.Equal(UpdateStage.Downloading, u.Stage);
        Assert.Equal("Downloading 0.3.1…", u.StatusText);
        e.HoldDownload.SetResult();
        await check;
        Assert.Equal(UpdateStage.Ready, u.Stage);
        Assert.Equal("0.3.1 is ready", u.StatusText);
        Assert.Equal("0.3.1", u.ReadyVersion);
        Assert.True(changes >= 3);
        u.RestartToUpdate();
        Assert.Equal(1, e.Applies);
    }

    [AvaloniaFact]
    public async Task ReadyUpdateIsNotDownloadedAgain()
    {
        var e = new FakeEngine { Found = "0.3.1" };
        using var u = new Updater(State(), e, autoChecks: false);
        await u.CheckNow();
        await u.CheckNow();
        Assert.Equal(2, e.Checks);
        Assert.Equal(1, e.Downloads);
        Assert.Equal(UpdateStage.Ready, u.Stage);
        e.Found = "0.3.2";
        await u.CheckNow();
        Assert.Equal(2, e.Downloads);
        Assert.Equal("0.3.2", u.ReadyVersion);
    }

    [AvaloniaFact]
    public async Task CheckFailureShowsMessageAndRetriesLater()
    {
        var e = new FakeEngine { CheckError = new HttpRequestException("rate limit exceeded (403)\nsecond line") };
        using var u = new Updater(State(), e) { FirstDelay = TimeSpan.FromMilliseconds(20), Interval = TimeSpan.FromMilliseconds(40) };
        await Until(() => u.Stage == UpdateStage.Failed);
        Assert.Equal("Couldn't check: rate limit exceeded (403)", u.StatusText);
        Assert.True(u.CanCheck);
        await Until(() => e.Checks >= 2); // the timer keeps running
        e.CheckError = null;
        await Until(() => u.Stage == UpdateStage.UpToDate);
    }

    [AvaloniaFact]
    public async Task FailedRecheckKeepsTheReadyUpdate()
    {
        var e = new FakeEngine { Found = "0.3.1" };
        using var u = new Updater(State(), e, autoChecks: false);
        await u.CheckNow();
        e.CheckError = new HttpRequestException("offline");
        await u.CheckNow();
        Assert.Equal("0.3.1", u.ReadyVersion);
        Assert.Equal(UpdateStage.Ready, u.Stage); // ponytail: stays Ready, only LastChecked moves; the restart items stay
        u.RestartToUpdate();
        Assert.Equal(1, e.Applies);
    }

    [AvaloniaFact]
    public void RestartWhenNotReadyDoesNothing()
    {
        var e = new FakeEngine { IsInstalled = false };
        using var u = new Updater(State(), e, autoChecks: false);
        u.RestartToUpdate();
        Assert.Equal(UpdateStage.Unavailable, u.Stage);
        Assert.Equal(0, e.Applies);
    }

    [AvaloniaFact]
    public async Task ConcurrentChecksRunOnce()
    {
        var e = new FakeEngine { HoldCheck = new() };
        using var u = new Updater(State(), e, autoChecks: false);
        var first = u.CheckNow();
        Assert.Equal(UpdateStage.Checking, u.Stage);
        Assert.False(u.CanCheck);
        await u.CheckNow();
        Assert.Equal(1, e.Checks);
        e.HoldCheck.SetResult();
        await first;
        Assert.Equal(1, e.Checks);
        Assert.True(u.CanCheck);
    }

    [AvaloniaFact]
    public async Task ToggleStopsAndRestartsTimer()
    {
        var e = new FakeEngine();
        var state = State();
        using var u = new Updater(state, e) { FirstDelay = TimeSpan.FromMilliseconds(30), Interval = TimeSpan.FromMilliseconds(30) };
        state.Settings.AutomaticUpdateChecks = false;
        state.Save();
        await Task.Delay(150);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, e.Checks);
        state.Settings.AutomaticUpdateChecks = true;
        state.Save();
        Assert.Equal(0, e.Checks); // scheduled, not immediate
        await Until(() => e.Checks >= 1);
    }

    [AvaloniaFact]
    public async Task NotInstalledNeverChecks()
    {
        var e = new FakeEngine { IsInstalled = false };
        using var u = new Updater(State(), e) { FirstDelay = TimeSpan.FromMilliseconds(10) };
        Assert.Equal(UpdateStage.Unavailable, u.Stage);
        Assert.Equal("Updates work in installed copies.", u.StatusText);
        Assert.False(u.CanCheck);
        await u.CheckNow();
        await Task.Delay(100);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, e.Checks);
        Assert.Equal(UpdateStage.Unavailable, u.Stage);
    }

    [AvaloniaFact]
    public async Task DownloadFailureIsNotReady()
    {
        var e = new FakeEngine { Found = "0.3.1", DownloadError = new IOException("connection reset") };
        using var u = new Updater(State(), e, autoChecks: false);
        await u.CheckNow();
        Assert.Equal(UpdateStage.Failed, u.Stage);
        Assert.Equal("Couldn't check: connection reset", u.StatusText);
        Assert.Null(u.ReadyVersion);
        e.DownloadError = null;
        await u.CheckNow(); // starts over
        Assert.Equal(2, e.Downloads);
        Assert.Equal(UpdateStage.Ready, u.Stage);
    }
}
