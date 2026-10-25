using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Weekmark.Core;
using MilestoneMath = Weekmark.Core.Milestones;

namespace Weekmark.App.Widget;

public sealed record HeaderModel(string Week, string Range, string Year, double YearSweep, string OfWeeks, string WeeksLeft, string Description);
public sealed record ChipModel(string Text, Geometry Icon, string? Tip);
public sealed record DayModel(string Weekday, string Number, bool IsToday, bool IsWeekend, bool IsPast, bool IsHoliday, string? Tip);
public sealed record ProgressModel(string Today, string Percent, double FillWidth);
public sealed record MilestoneModel(string Name, string WeekText, string? Countdown, IBrush Brush, WeekRef Week, bool CanPin, string Tip);
public sealed record MonthDayModel(string Number, bool IsToday, bool OutOfMonth, bool IsHoliday);
public sealed record DetailsModel(string Title, string Subtitle, bool IsHint, bool IsPinned, bool IsCurrent, bool IsError = false);

public sealed partial class MonthRowModel(WeekRef week, IReadOnlyList<MonthDayModel> days, bool isCurrent, bool sprintStart,
    IBrush? flag, string? tip) : ObservableObject
{
    public WeekRef Week { get; } = week;
    public string WeekText { get; } = week.Week.ToString();
    public IReadOnlyList<MonthDayModel> Days { get; } = days;
    public bool IsCurrent { get; } = isCurrent;
    public bool SprintStart { get; } = sprintStart;
    public IBrush? Flag { get; } = flag;
    public bool HasFlag => Flag is not null;
    public string? Tip { get; } = tip;
    [ObservableProperty] private bool isPinned;
    [ObservableProperty] private bool isHover;
}

/// <summary>Everything the widget shows, port of the Mac's WidgetView + Browser state.</summary>
public sealed partial class WidgetViewModel : ObservableObject, IDisposable
{
    const double ContentWidth = 264; // 300 wide minus 2 × 18 padding
    static readonly TimeSpan IdleReset = TimeSpan.FromMinutes(2); // as on the Mac

    readonly AppState state;
    WeekSnapshot snap = null!;
    (DateOnly, int)? fullKey;
    (DateOnly, DateOnly, int)? monthKey;
    WeekNumbering? numbering;
    // ponytail: evaluated on the UI thread once typing pauses; a huge range (2000 w1-9998 w52) blocks ~0.5 s once.
    // Off-thread evaluation needs a thread-safe HolidayStore cache first.
    readonly DispatcherTimer debounce = new() { Interval = TimeSpan.FromMilliseconds(150) };
    DateTime lastInteraction;
    WeekRef? hover;
    bool glassAvailable, silent;

    public WidgetViewModel(AppState state)
    {
        this.state = state;
        lastInteraction = Now;
        debounce.Tick += (_, _) => FlushSearch();
        state.Changed += Refresh;
        Refresh();
    }

    /// <summary>
    /// Raised by <see cref="Reset"/> (Esc, ×, Today, idle): the window gives keyboard focus back. The argument is false
    /// for the idle reset, which must not hand the foreground to another app.
    /// </summary>
    public event Action<bool>? WasReset;

    /// <summary>The lookup; tests swap it for one that throws.</summary>
    internal Func<string, WeekContext, IReadOnlyList<QueryResult>> Evaluate { get; set; } = QueryEngine.Evaluate;

    AppSettings S => state.Settings;
    WeekContext Ctx => state.Context;

    [ObservableProperty] private double scale = 1;
    [ObservableProperty] private HeaderModel header = null!;
    [ObservableProperty] private bool showPlanning;
    [ObservableProperty] private IReadOnlyList<ChipModel> chips = [];
    [ObservableProperty] private IReadOnlyList<DayModel> days = [];
    [ObservableProperty] private bool showProgress;
    [ObservableProperty] private ProgressModel progress = null!;
    [ObservableProperty] private bool showMilestones;
    [ObservableProperty] private IReadOnlyList<MilestoneModel> milestones = [];
    [ObservableProperty] private bool showMonth;
    [ObservableProperty] private string monthTitle = "";
    [ObservableProperty] private IReadOnlyList<string> weekdays = [];
    [ObservableProperty] private IReadOnlyList<MonthRowModel> rows = [];
    [ObservableProperty] private bool isBrowsing;
    [ObservableProperty] private DetailsModel details = Hint;
    [ObservableProperty] private bool isGlass;
    [ObservableProperty] private bool isSolid;
    [ObservableProperty] private bool isClear;
    [ObservableProperty] private int monthOffset;
    [ObservableProperty] private WeekRef? pinned;
    /// <summary>The search field's text; evaluated 150 ms after the last keystroke.</summary>
    [ObservableProperty] private string query = "";
    /// <summary>The first lookup result for <see cref="Query"/>; its week is pinned (orange) and shown in the month grid.</summary>
    [ObservableProperty] private QueryResult? result;

    /// <summary>Set by the window: true when the OS gave it Mica/acrylic/blur. Glass falls back to Solid otherwise.</summary>
    public bool GlassAvailable
    {
        get => glassAvailable;
        set { glassAvailable = value; UpdateBackground(); }
    }

    public WeekRef? Hover
    {
        get => hover;
        set
        {
            if (hover == value) return;
            hover = value;
            OnPropertyChanged();
            UpdateSelection();
        }
    }

    /// <summary>A click on a week or milestone: pins it and clears the query, as the Mac's <c>select</c>.</summary>
    public void Pin(WeekRef r)
    {
        if (!IsValid(r)) return; // stored or computed weeks that don't exist
        ClearSearch();
        Pinned = r;
        MonthOffset = snap.MonthOffsetFor(r);
        Touch();
        RebuildMonth();
    }

    public void Step(int delta)
    {
        MonthOffset = Math.Clamp(MonthOffset + delta, -1200, 1200);
        Touch();
        RebuildMonth();
    }

    [RelayCommand] void Previous() => Step(-1);
    [RelayCommand] void Next() => Step(1);

    /// <summary>Clears the query, the pin and the month offset (Esc, ×, Today, 2 minutes idle).</summary>
    [RelayCommand]
    public void Reset() => Reset(handBack: true);

    void Reset(bool handBack)
    {
        ClearSearch();
        Pinned = null;
        hover = null;
        OnPropertyChanged(nameof(Hover));
        MonthOffset = 0;
        RebuildMonth();
        WasReset?.Invoke(handBack);
    }

    partial void OnQueryChanged(string value)
    {
        if (silent) return;
        Touch();
        debounce.Stop();
        if (value.Trim().Length > 0) { debounce.Start(); UpdateSelection(); return; }
        Result = null; // emptied by hand: back to today at once
        Pinned = null;
        MonthOffset = 0;
        RebuildMonth();
    }

    /// <summary>Evaluates a pending query now (Enter, tests); no-op when nothing is pending.</summary>
    public void FlushSearch()
    {
        if (!debounce.IsEnabled) return;
        debounce.Stop();
        Search();
    }

    /// <summary>
    /// Runs from the debounce, Enter and every clock tick or settings save (via <see cref="RebuildAll"/>), so it never
    /// throws. <paramref name="keepMonth"/>: a re-search after a new day or a settings save keeps the month the user
    /// browsed to.
    /// </summary>
    void Search(bool keepMonth = false)
    {
        if (Query.Trim().Length == 0) return;
        try
        {
            Result = Evaluate(Query, Ctx).FirstOrDefault();
            if (Result?.Week is { } w && IsValid(w)) // settings may make a week invalid: never pin or scroll to it
            {
                Pinned = w;
                if (!keepMonth) MonthOffset = snap.MonthOffsetFor(w);
            }
            else Pinned = null;
        }
        catch (Exception) // a lookup bug must not take the widget down: say so like any other non-answer
        {
            Result = new(QueryKind.Error, $"No match for “{Query.Trim()}”", QueryEngine.Examples);
            Pinned = null;
        }
        RebuildMonth();
    }

    void ClearSearch()
    {
        debounce.Stop();
        Result = null;
        silent = true;
        Query = "";
        silent = false;
    }

    // UTC: a DST change never makes the idle time negative.
    DateTime Now => state.Now.ToUniversalTime();
    void Touch() => lastInteraction = Now;

    bool IsValid(WeekRef r) => r.Year is >= 2 and <= 9998 && r.Week >= 1 && r.Week <= Ctx.Cal.WeeksIn(r.Year);

    void Refresh()
    {
        snap = new WeekSnapshot(Ctx.Now, Ctx.Cal, Ctx.Fmt);
        var key = (Ctx.Today, state.Version);
        if (key != fullKey) { fullKey = key; RebuildAll(); }
        else Progress = BuildProgress(); // a plain 30 s tick: only the week progress moves
        if (IsBrowsing && Now - lastInteraction > IdleReset) Reset(handBack: false);
    }

    void RebuildAll()
    {
        if (numbering is { } n && n != Ctx.Cal.Numbering) { Pinned = null; hover = null; MonthOffset = 0; } // as on the Mac
        numbering = Ctx.Cal.Numbering;
        var fmt = Ctx.Fmt;
        Scale = S.Size.Scale();
        var cur = snap.Current;
        Header = new(cur.Week.ToString(), snap.RangeText, cur.Year.ToString(), 360 * snap.YearProgress,
            $"of {snap.WeeksInYear}", $"{snap.WeeksLeft} wks left",
            $"Calendar week {cur.Week}, {snap.RangeText}. Week {cur.Week} of {snap.WeeksInYear}, {snap.WeeksLeft} weeks left in {cur.Year}");
        Chips = BuildChips();
        ShowPlanning = S.ShowPlanning && Chips.Count > 0;
        Days = snap.Days.Select(d =>
        {
            var hol = Ctx.Holidays.On(Day.From(d));
            return new DayModel(fmt.Format(d, DateTemplate.Weekday).ToUpperInvariant(), d.Day.ToString(), snap.IsToday(d),
                snap.IsWeekend(d), snap.IsPast(d), hol.Count > 0, hol.Count > 0 ? string.Join(", ", hol.Select(h => h.Name)) : null);
        }).ToList();
        ShowProgress = S.ShowProgress;
        Progress = BuildProgress();
        Milestones = BuildMilestones();
        ShowMilestones = S.ShowMilestones && Milestones.Count > 0;
        ShowMonth = S.ShowMonth;
        Weekdays = snap.Days.Select(d => fmt.Format(d, DateTemplate.Weekday)[..1]).ToList();
        UpdateBackground();
        RebuildMonth();
        if (!debounce.IsEnabled) Search(keepMonth: true); // a new day or new settings: "+1w" or "51" may mean another week now
    }

    List<ChipModel> BuildChips()
    {
        var list = new List<ChipModel>();
        if (Quarters.Info(Ctx.Today, S.Quarter, Ctx.Cal) is { } q) list.Add(new(q.Label, Icons.Quarter, null));
        if (Sprints.Info(Ctx.Today, S.Sprint, Ctx.Cal, Ctx.Holidays) is { } sp)
        {
            list.Add(new($"{S.Sprint.Name} {sp.Number} · day {Math.Max(sp.Workday, 1)}/{sp.WorkdaysTotal}", Icons.Sprint,
                $"CW{sp.FirstWeek.Week}–CW{sp.LastWeek.Week}"));
            if (sp.Pi is { } pi && sp.SprintInPI is { } n) list.Add(new($"PI {pi} · {n}/{sp.SprintsPerPI}", Icons.Pi, null));
        }
        return list;
    }

    ProgressModel BuildProgress() => new(snap.TodayText,
        $"{(int)Math.Round(snap.WeekProgress * 100, MidpointRounding.AwayFromZero)}% of week",
        Math.Max(5, ContentWidth * snap.WeekProgress));

    List<MilestoneModel> BuildMilestones() =>
        MilestoneMath.Upcoming(S.Milestones, Ctx.Today, Ctx.Cal).Take(3).Select(m =>
        {
            // Status is null exactly when the stored week doesn't exist: no countdown, no pinning, no date range.
            var st = MilestoneMath.Status(m, Ctx.Today, Ctx.Cal, Ctx.Holidays);
            var countdown = st is null ? null : st.WeeksAway == 0 ? $"{st.WorkdaysLeft}d left" : $"{st.WeeksAway}w · {st.WorkdaysLeft}d";
            return new MilestoneModel(m.Name, $"CW{m.Week.Week}", countdown, BrushFor(m.Color), m.Week, st is not null,
                st is null ? m.Name : $"{m.Name} — {Ctx.Fmt.LongRange(m.Week)}");
        }).ToList();

    void RebuildMonth()
    {
        var month = snap.DisplayedMonth(MonthOffset);
        var key = (month, Ctx.Today, state.Version);
        if (key != monthKey) // rebuilding the rows under the pointer would drop the hover, so only when they change
        {
            monthKey = key;
            MonthTitle = snap.MonthTitle(month);
            var byWeek = S.Milestones.GroupBy(m => m.Week).ToDictionary(g => g.Key, g => g.ToList());
            Rows = snap.MonthRows(month).Select(r =>
            {
                var ms = byWeek.GetValueOrDefault(r.Ref);
                var days = r.Days.Select(d => new MonthDayModel(d.Day.ToString(), snap.IsToday(d), !snap.IsIn(d, month),
                    Ctx.Holidays.IsHoliday(Day.From(d)))).ToList();
                return new MonthRowModel(r.Ref, days, r.IsCurrent, Sprints.StartsSprint(r.Ref, S.Sprint, Ctx.Cal),
                    ms is null ? null : BrushFor(ms[0].Color), ms is null ? null : string.Join(", ", ms.Select(m => m.Name)));
            }).ToList();
        }
        UpdateSelection();
    }

    void UpdateSelection()
    {
        foreach (var r in Rows)
        {
            r.IsPinned = r.Week == Pinned;
            r.IsHover = r.Week == hover;
        }
        IsBrowsing = MonthOffset != 0 || Pinned is not null || Query.Length > 0;
        if (hover is null && Result is { } q) // hover wins over the lookup result, as on the Mac
        {
            Details = new(q.Title, q.Detail is { } qd ? $"{q.Subtitle} · {qd}" : q.Subtitle, false,
                q.Week is { } qw && qw == Pinned, q.Week == Ctx.Current, q.Kind == QueryKind.Error);
            return;
        }
        var shown = hover ?? Pinned;
        if (shown is not { } w || !IsValid(w)) { Details = Hint; return; }
        var d = WeekDetails.For(w, Ctx);
        Details = new(d.Title, d.Detail is { } x ? $"{d.Subtitle} · {x}" : d.Subtitle, false, hover is null && w == Pinned, w == Ctx.Current);
    }

    void UpdateBackground()
    {
        var b = S.Background == BackgroundStyle.Glass && !glassAvailable ? BackgroundStyle.Solid : S.Background;
        IsGlass = b == BackgroundStyle.Glass;
        IsSolid = b == BackgroundStyle.Solid;
        IsClear = b == BackgroundStyle.Clear;
    }

    static readonly DetailsModel Hint = new("", "Hover a week for details · click to pin it", true, false, false);

    // SwiftUI's system colours, which the Mac widget uses for milestones.
    internal static IBrush BrushFor(MilestoneColor c) => new ImmutableSolidColorBrush(Color.Parse(c switch
    {
        MilestoneColor.Red => "#FF3B30",
        MilestoneColor.Pink => "#FF2D55",
        MilestoneColor.Purple => "#AF52DE",
        MilestoneColor.Blue => "#007AFF",
        MilestoneColor.Teal => "#30B0C7",
        MilestoneColor.Green => "#34C759",
        _ => "#FF9500",
    }));

    public void Dispose()
    {
        state.Changed -= Refresh;
        debounce.Stop();
    }

    // Created on first use, after Avalonia's platform is up (geometry parsing needs it).
    static class Icons
    {
        public static readonly Geometry Quarter = Geometry.Parse("M5,1 A4,4 0 1 0 9,5 M5,1 V5 H9 M6.5,0.5 A3.5,3.5 0 0 1 9.5,3.5");
        public static readonly Geometry Sprint = Geometry.Parse("M1.5,5 A3.5,3.5 0 0 1 8.2,3.2 M8.4,0.8 V3.4 H5.8 M8.5,5 A3.5,3.5 0 0 1 1.8,6.8 M1.6,9.2 V6.6 H4.2");
        public static readonly Geometry Pi = Geometry.Parse("M5,1 L9,3 L5,5 L1,3 Z M1,5 L5,7 L9,5 M1,7 L5,9 L9,7");
    }
}
