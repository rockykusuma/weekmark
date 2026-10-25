using System.Globalization;
using Avalonia.Threading;
using Weekmark.Core;

namespace Weekmark.App;

/// <summary>
/// The app's settings and "now". <see cref="Changed"/> fires when settings are saved and when the clock ticks
/// (every 30 s, at local midnight, and when Windows reports a time change or resume via <see cref="Tick"/>).
/// </summary>
public sealed class AppState : IDisposable
{
    readonly Func<DateTime> clock;
    readonly DispatcherTimer ticker = new() { Interval = TimeSpan.FromSeconds(30) };
    readonly DispatcherTimer midnight = new();

    /// <param name="store">Null in demo mode: nothing is ever read from or written to disk.</param>
    public AppState(AppSettings settings, SettingsStore? store, Func<DateTime>? clock = null)
    {
        Settings = settings;
        Store = store;
        this.clock = clock ?? (() => DateTime.Now);
        Context = Build();
        if (store is not null) store.Saved += _ => SettingsChanged();
        ticker.Tick += (_, _) => Tick();
        midnight.Tick += (_, _) => Tick();
    }

    /// <summary><c>--demo-data</c> uses in-memory sample settings and never constructs a store on <paramref name="settingsPath"/>.</summary>
    public static AppState FromOptions(Options o, string settingsPath)
    {
        if (o.DemoData) return new AppState(DemoData.Create(DateOnly.FromDateTime(DateTime.Now)), null);
        var store = new SettingsStore(settingsPath);
        return new AppState(store.Load(), store);
    }

    public AppSettings Settings { get; }
    public SettingsStore? Store { get; }
    public WeekContext Context { get; private set; }
    /// <summary>The clock right now (tests inject theirs); <see cref="Context"/> holds the last tick's time.</summary>
    public DateTime Now => clock();
    /// <summary>Bumped on every settings change, so views can tell a settings change from a clock tick.</summary>
    public int Version { get; private set; }
    public event Action? Changed;

    public void Start()
    {
        ticker.Start();
        ScheduleMidnight();
    }

    /// <summary>Persists <see cref="Settings"/> (a read-only store keeps the change in memory) and refreshes.</summary>
    public void Save()
    {
        if (Store is null || !Store.Save(Settings)) SettingsChanged(); // a successful Save raises Saved instead
    }

    public void Tick()
    {
        TimeZoneInfo.ClearCachedData(); // DateTime.Now follows a time-zone change
        Context = Context with { Now = clock() };
        ScheduleMidnight();
        Changed?.Invoke();
    }

    void SettingsChanged()
    {
        Version++;
        Context = Build();
        Changed?.Invoke();
    }

    WeekContext Build()
    {
        var cal = Settings.BuildCalendar();
        var fmt = new WeekFormatter(cal, WeekFormatter.DayFirstFor(CultureInfo.CurrentCulture));
        return new WeekContext(cal, fmt, clock(), Settings.BuildHolidays(), Settings.Milestones, Settings.Sprint);
    }

    void ScheduleMidnight()
    {
        if (!ticker.IsEnabled) return; // not started (tests, screenshots of a fixed clock)
        midnight.Stop();
        var now = DateTime.Now;
        midnight.Interval = WeekClock.NextMidnight(now) - now + TimeSpan.FromSeconds(1);
        midnight.Start();
    }

    public void Dispose()
    {
        ticker.Stop();
        midnight.Stop();
    }
}
