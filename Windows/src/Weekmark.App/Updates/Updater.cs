using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Velopack;
using Velopack.Sources;

namespace Weekmark.App.Updates;

public enum UpdateStage { Unavailable, Idle, Checking, UpToDate, Downloading, Ready, Failed }

public interface IUpdateEngine
{
    bool IsInstalled { get; }
    string? CurrentVersion { get; }
    /// <summary>The newer version, or null when up to date. Remembered for <see cref="DownloadAsync"/> and <see cref="ApplyAndRestart"/>.</summary>
    Task<string?> CheckAsync();
    Task DownloadAsync(Action<int> progress);
    void ApplyAndRestart();
}

/// <summary>Velopack against the GitHub releases. Never throws from <see cref="IsInstalled"/>: a copy that cannot update reports false.</summary>
internal sealed class VelopackEngine : IUpdateEngine
{
    readonly UpdateManager? manager = Create();
    UpdateInfo? info;

    static UpdateManager? Create()
    {
        try
        {
            var m = new UpdateManager(new GithubSource("https://github.com/rockykusuma/weekmark", null, prerelease: true));
            return m.IsInstalled ? m : null;
        }
        catch { return null; }
    }

    public bool IsInstalled => manager is not null;
    public string? CurrentVersion => manager?.CurrentVersion?.ToString();

    public async Task<string?> CheckAsync()
    {
        info = await manager!.CheckForUpdatesAsync();
        return info?.TargetFullRelease.Version.ToString();
    }

    public Task DownloadAsync(Action<int> progress) => manager!.DownloadUpdatesAsync(info!, progress);

    public void ApplyAndRestart() => manager!.ApplyUpdatesAndRestart(info!.TargetFullRelease);
}

/// <summary>
/// Checks for a newer release once after start and then daily (while the setting is on), downloads it, and
/// offers to restart into it. Velopack installs a downloaded update on the next start anyway.
/// </summary>
public sealed partial class Updater : ObservableObject, IDisposable
{
    readonly AppState state;
    readonly IUpdateEngine engine;
    readonly DispatcherTimer timer = new();
    string? checkingVersion, failure;
    DateTime? lastChecked;
    bool firstDone;

    [ObservableProperty] UpdateStage stage;

    public Updater(AppState state, IUpdateEngine engine, bool autoChecks = true)
    {
        this.state = state;
        this.engine = engine;
        bool installed;
        try { installed = engine.IsInstalled; } catch { installed = false; }
        stage = installed ? UpdateStage.Idle : UpdateStage.Unavailable;
        if (!installed || !autoChecks) return;
        timer.Tick += async (_, _) =>
        {
            firstDone = true;
            timer.Interval = Interval;
            await CheckNow();
        };
        state.Changed += Reschedule;
        Reschedule();
    }

    /// <summary>Tests shorten these. Changing FirstDelay reschedules a first check that is still waiting.</summary>
    internal TimeSpan FirstDelay
    {
        get;
        set { field = value; if (timer.IsEnabled && !firstDone) Start(); }
    } = TimeSpan.FromSeconds(60);
    internal TimeSpan Interval { get; set; } = TimeSpan.FromHours(24);

    public event Action? Changed;
    public string? ReadyVersion { get; private set; }
    public bool CanCheck => Stage is not (UpdateStage.Unavailable or UpdateStage.Checking or UpdateStage.Downloading);

    public string StatusText => Stage switch
    {
        UpdateStage.Unavailable => "Updates work in installed copies.",
        UpdateStage.Checking => "Checking…",
        UpdateStage.UpToDate => $"Weekmark {engine.CurrentVersion} is the latest version",
        UpdateStage.Downloading => $"Downloading {checkingVersion}…",
        UpdateStage.Ready => $"{ReadyVersion} is ready",
        UpdateStage.Failed => $"Couldn't check: {failure}",
        _ => "",
    };

    public string LastCheckedText => lastChecked is not { } t ? "Last checked: never"
        : t.Date == state.Now.Date ? $"Last checked: {t:HH:mm}"
        : $"Last checked: {t:d} {t:HH:mm}";

    void Reschedule()
    {
        var on = state.Settings.AutomaticUpdateChecks;
        if (on && !timer.IsEnabled) { firstDone = false; Start(); }
        else if (!on && timer.IsEnabled) timer.Stop();
    }

    void Start()
    {
        timer.Stop();
        timer.Interval = FirstDelay;
        timer.Start();
    }

    public async Task CheckNow()
    {
        if (!CanCheck) return;
        Set(UpdateStage.Checking);
        try
        {
            var version = await engine.CheckAsync();
            if (version is null) { lastChecked = state.Now; ReadyVersion = null; Set(UpdateStage.UpToDate); return; }
            if (version != ReadyVersion)
            {
                checkingVersion = version;
                ReadyVersion = null;
                Set(UpdateStage.Downloading);
                await engine.DownloadAsync(_ => { });
                ReadyVersion = version;
            }
            lastChecked = state.Now;
            Set(UpdateStage.Ready);
        }
        catch (Exception e)
        {
            lastChecked = state.Now;
            failure = e.Message.Split('\n')[0].Trim();
            // A downloaded update stays offered when a later check fails (offline laptop); a failed download never got that far.
            Set(ReadyVersion is null ? UpdateStage.Failed : UpdateStage.Ready);
        }
    }

    public void RestartToUpdate()
    {
        if (Stage != UpdateStage.Ready) return;
        try { engine.ApplyAndRestart(); }
        catch (Exception e)
        {
            failure = e.Message.Split('\n')[0].Trim();
            Set(UpdateStage.Failed);
        }
    }

    void Set(UpdateStage s)
    {
        Stage = s;
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ReadyVersion));
        OnPropertyChanged(nameof(LastCheckedText));
        OnPropertyChanged(nameof(CanCheck));
        Changed?.Invoke();
    }

    public void Dispose()
    {
        timer.Stop();
        state.Changed -= Reschedule;
    }
}
