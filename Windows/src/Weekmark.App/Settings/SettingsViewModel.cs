using System.Reflection;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Weekmark.App.Platform;
using Weekmark.App.Widget;
using Weekmark.Core;
using MilestoneMath = Weekmark.Core.Milestones;

namespace Weekmark.App.Settings;

public sealed partial class RegionItem(HolidayRegion region, Func<HolidayRegion, bool> get, Action<HolidayRegion, bool> set) : ObservableObject
{
    public string Label { get; } = $"{region.Flag()} {region.Title()}";
    public bool IsOn { get => get(region); set => set(region, value); }
    internal void Notify() => OnPropertyChanged(nameof(IsOn));
}

public sealed record SourceItem(string Name, string Count, IRelayCommand Remove);
public sealed record MilestoneItem(string Name, string Detail, string? Countdown, IBrush Brush, IRelayCommand Edit, IRelayCommand Delete);

/// <summary>The milestone being added or edited. Weeks are checked against the numbering in use.</summary>
public sealed partial class MilestoneDraft(Guid? id, WeekCalendar cal, WeekFormatter fmt) : ObservableObject
{
    public Guid? Id { get; } = id;
    public string Title => Id is null ? "New milestone" : "Edit milestone";

    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanSave))] private string name = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Dates), nameof(Error), nameof(CanSave))] private int year;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Dates), nameof(Error), nameof(CanSave))] private int week;
    [ObservableProperty] private int colorIndex;

    public string? Error => Year is < 2 or > 9998 ? "Pick a year from 2 to 9998"
        : Week < 1 || Week > cal.WeeksIn(Year) ? $"{Year} has only {cal.WeeksIn(Year)} weeks" : null;
    public string? Dates => Error is null ? fmt.LongRange(new WeekRef(Year, Week)) : null;
    public bool CanSave => Error is null && !string.IsNullOrWhiteSpace(Name);
}

/// <summary>Every settings tab. Each edit is saved straight away, as on the Mac.</summary>
public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    public const string ReadOnlyWarning = "Weekmark couldn't read its settings file, so changes won't be saved until you restart it.";

    public static string[] Numberings { get; } = ["ISO 8601 (Monday start)", "US (Sunday start)"];
    public static string[] Appearances { get; } = ["System", "Light", "Dark"];
    public static string[] Layers { get; } = ["On Desktop (behind windows)", "Float Above Windows"];
    public static string[] Sizes { get; } = ["Small", "Medium", "Large", "Extra Large"];
    public static string[] Backgrounds { get; } = ["Glass", "Solid", "Clear"];
    public static string[] QuarterModes { get; } = ["By month", "By week (13-week quarters)"];
    public static string[] Months { get; } = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
    public static string[] Colors { get; } = Enum.GetNames<MilestoneColor>();
    public static IBrush[] ColorBrushes { get; } = Enum.GetValues<MilestoneColor>().Select(WidgetViewModel.BrushFor).ToArray();
    public static string Version { get; } = "Version " + (typeof(SettingsViewModel).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "dev");

    readonly AppState state;
    readonly string? startupExe;
    int seenVersion;

    /// <param name="startupExe">The installed Weekmark.exe to register for "Start with Windows"; null leaves the Run key alone.</param>
    public SettingsViewModel(AppState state, string? startupExe = null)
    {
        this.state = state;
        this.startupExe = startupExe;
        Regions = Enum.GetValues<HolidayRegion>().Select(r => new RegionItem(r, S.Regions.Contains, SetRegion)).ToList();
        AddMilestone = new RelayCommand(() => Draft = NewDraft(null, Ctx.Current, ""));
        SaveDraft = new RelayCommand(SaveMilestone);
        CancelDraft = new RelayCommand(() => Draft = null);
        state.Changed += OnStateChanged;
        Refresh();
    }

    AppSettings S => state.Settings;
    WeekContext Ctx => state.Context;

    public string? SaveWarning => state.Store switch
    {
        { IsReadOnly: true } => ReadOnlyWarning,
        { LastSaveError: { } e } => $"Couldn't save settings: {e.TrimEnd('.')}. Your changes will be kept until Weekmark quits.",
        _ => null,
    };

    // Only a real change saves: a control echoing back the value it was given, or a list's -1 while it loads, never
    // rewrites the file (stored values the controls can't represent stay as they are until the user edits them).
    void Set<T>(T current, T value, Action<T> apply)
    {
        if (!EqualityComparer<T>.Default.Equals(current, value)) Edit(() => apply(value));
    }
    void Pick<T>(int index, T current, Action<T> apply) where T : struct, Enum
    {
        if (index >= 0 && index < Enum.GetValues<T>().Length) Set(current, (T)(object)index, apply);
    }
    void EditSprint(SprintConfig c) => Set(S.Sprint, c, v => S.Sprint = v);
    void EditQuarter(QuarterConfig c) => Set(S.Quarter, c, v => S.Quarter = v);

    // General
    public int NumberingIndex { get => (int)S.Numbering; set => Pick(value, S.Numbering, v => S.Numbering = v); }
    public int AppearanceIndex { get => (int)S.Appearance; set => Pick(value, S.Appearance, v => S.Appearance = v); }
    public bool ShowInTray { get => S.ShowInMenuBar; set => Set(S.ShowInMenuBar, value, v => S.ShowInMenuBar = v); }
    public bool StartWithWindows
    {
        get => S.StartWithWindows;
        set
        {
            if (value == S.StartWithWindows) return;
            Edit(() => S.StartWithWindows = value);
            if (startupExe is not null) Startup.Apply(value, startupExe);
        }
    }

    // Widget
    public int LayerIndex { get => (int)S.Layer; set => Pick(value, S.Layer, v => S.Layer = v); }
    public bool Locked { get => S.Locked; set => Set(S.Locked, value, v => S.Locked = v); }
    public int SizeIndex { get => (int)S.Size; set => Pick(value, S.Size, v => S.Size = v); }
    public int BackgroundIndex { get => (int)S.Background; set => Pick(value, S.Background, v => S.Background = v); }
    public bool ShowPlanning { get => S.ShowPlanning; set => Set(S.ShowPlanning, value, v => S.ShowPlanning = v); }
    public bool ShowProgress { get => S.ShowProgress; set => Set(S.ShowProgress, value, v => S.ShowProgress = v); }
    public bool ShowMilestones { get => S.ShowMilestones; set => Set(S.ShowMilestones, value, v => S.ShowMilestones = v); }
    public bool ShowMonth { get => S.ShowMonth; set => Set(S.ShowMonth, value, v => S.ShowMonth = v); }

    // Planning. User edits are clamped (length and sprints per PI match SettingsStore's clamps and Sprints' limits);
    // an unchanged value is never clamped, so a stored -3 or 12000 stays until the user edits that field.
    public bool SprintEnabled { get => S.Sprint.Enabled; set => EditSprint(S.Sprint with { Enabled = value }); }
    /// <summary>Bound with UpdateSourceTrigger=LostFocus: saved when the field is left, not on every keystroke.</summary>
    public string SprintName { get => S.Sprint.Name; set => EditSprint(S.Sprint with { Name = value }); }
    public int SprintLength { get => S.Sprint.LengthWeeks; set { if (value != S.Sprint.LengthWeeks) EditSprint(S.Sprint with { LengthWeeks = Math.Clamp(value, 1, 52) }); } }
    public int SprintFirstNumber { get => S.Sprint.FirstNumber; set { if (value != S.Sprint.FirstNumber) EditSprint(S.Sprint with { FirstNumber = Math.Clamp(value, 0, 9999) }); } }
    public int SprintsPerPI { get => S.Sprint.SprintsPerPI; set { if (value != S.Sprint.SprintsPerPI) EditSprint(S.Sprint with { SprintsPerPI = Math.Clamp(value, 0, 100) }); } }
    public int FirstPI { get => S.Sprint.FirstPI; set { if (value != S.Sprint.FirstPI) EditSprint(S.Sprint with { FirstPI = Math.Clamp(value, 0, 999) }); } }
    public bool ShowPI => S.Sprint.Enabled && S.Sprint.SprintsPerPI > 0;
    /// <summary>Null when the stored anchor is not a real date.</summary>
    public DateTime? SprintAnchor
    {
        get => S.Sprint.Anchor.TryToDate()?.ToDateTime(TimeOnly.MinValue);
        set { if (value is { } d) EditSprint(S.Sprint with { Anchor = Day.From(DateOnly.FromDateTime(d)) }); }
    }
    public string SprintPreview
    {
        get
        {
            if (Sprints.Info(Ctx.Today, S.Sprint, Ctx.Cal, Ctx.Holidays) is not { } i)
                return "Pick any past sprint's start date and number; the app counts forward from there.";
            var s = $"Now: {S.Sprint.Name} {i.Number} (CW{i.FirstWeek.Week}–CW{i.LastWeek.Week}), working day {Math.Max(i.Workday, 1)} of {i.WorkdaysTotal}";
            return i.Pi is { } pi && i.SprintInPI is { } n ? $"{s} · PI {pi}, sprint {n} of {i.SprintsPerPI}" : s;
        }
    }
    public bool QuarterEnabled { get => S.Quarter.Enabled; set => EditQuarter(S.Quarter with { Enabled = value }); }
    public int QuarterModeIndex { get => (int)S.Quarter.Mode; set => Pick(value, S.Quarter.Mode, v => S.Quarter = S.Quarter with { Mode = v }); }
    public int FiscalMonthIndex { get => S.Quarter.FiscalStartMonth - 1; set { if (value is >= 0 and < 12) EditQuarter(S.Quarter with { FiscalStartMonth = value + 1 }); } }
    public bool ShowFiscalMonth => S.Quarter.Enabled && S.Quarter.Mode == QuarterMode.Months;

    // Holidays
    public IReadOnlyList<RegionItem> Regions { get; }
    [ObservableProperty] private IReadOnlyList<SourceItem> sources = [];
    [ObservableProperty] private string? importMessage;

    /// <summary>Adds (or replaces) the holidays of one imported calendar. Returns the line shown under the button.</summary>
    public string ImportIcs(string text, string source)
    {
        var parsed = IcsParser.Parse(text, source);
        if (parsed.Count == 0) return ImportMessage = $"No holidays found in {source}";
        Edit(() =>
        {
            S.CustomHolidays.RemoveAll(h => h.Source == source);
            S.CustomHolidays.AddRange(parsed);
        });
        return ImportMessage = $"Imported {parsed.Count} holiday{(parsed.Count == 1 ? "" : "s")} from {source}";
    }

    void SetRegion(HolidayRegion r, bool on) => Edit(() =>
    {
        S.Regions.Remove(r);
        if (on) S.Regions.Add(r);
    });

    // Milestones
    [ObservableProperty] private IReadOnlyList<MilestoneItem> milestones = [];
    public bool NoMilestones => Milestones.Count == 0;
    [ObservableProperty] private MilestoneDraft? draft;
    public IRelayCommand AddMilestone { get; }
    public IRelayCommand SaveDraft { get; }
    public IRelayCommand CancelDraft { get; }

    MilestoneDraft NewDraft(Milestone? m, WeekRef week, string name) => new(m?.Id, Ctx.Cal, Ctx.Fmt)
    {
        Name = name, Year = week.Year, Week = week.Week, ColorIndex = (int)(m?.Color ?? MilestoneColor.Orange),
    };

    void SaveMilestone()
    {
        if (Draft is not { CanSave: true } d) return;
        var m = new Milestone(d.Id ?? Guid.NewGuid(), d.Name.Trim(), new WeekRef(d.Year, d.Week), (MilestoneColor)Math.Clamp(d.ColorIndex, 0, Colors.Length - 1));
        Draft = null;
        Edit(() =>
        {
            S.Milestones.RemoveAll(x => x.Id == m.Id);
            S.Milestones.Add(m);
            S.Milestones.Sort((a, b) => a.Week.CompareTo(b.Week));
        });
    }

    MilestoneItem Item(Milestone m)
    {
        var w = m.Week;
        var st = MilestoneMath.Status(m, Ctx.Today, Ctx.Cal, Ctx.Holidays); // null when the week doesn't exist
        var detail = st is null ? $"CW{w.Week} {w.Year} · this week doesn't exist" : $"CW{w.Week} {w.Year} · {Ctx.Fmt.ShortRange(w)}";
        var countdown = st is null ? null : st.IsPast ? "Done" : $"{st.WeeksAway}w · {st.WorkdaysLeft} working days";
        return new(m.Name, detail, countdown, WidgetViewModel.BrushFor(m.Color),
            new RelayCommand(() => Draft = NewDraft(m, w, m.Name)),
            new RelayCommand(() => Edit(() => S.Milestones.RemoveAll(x => x.Id == m.Id))));
    }

    /// <summary>Re-reads everything, e.g. after the tray's Size menu changed a setting.</summary>
    public void Refresh()
    {
        seenVersion = state.Version;
        Sources = S.CustomHolidays.GroupBy(h => h.Source).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new SourceItem(g.Key == "custom" ? "Added by you" : g.Key, $"{g.Count()} day{(g.Count() == 1 ? "" : "s")}",
                new RelayCommand(() => Edit(() => S.CustomHolidays.RemoveAll(h => h.Source == g.Key)))))
            .ToList();
        Milestones = S.Milestones.Select(Item).ToList();
        foreach (var r in Regions) r.Notify();
        OnPropertyChanged(string.Empty);
    }

    void Edit(Action apply)
    {
        apply();
        state.Save(); // a read-only store keeps the change in memory; SaveWarning says so
        Refresh();
    }

    void OnStateChanged()
    {
        if (state.Version != seenVersion) Refresh(); // a plain clock tick changes nothing here
    }

    public void Dispose() => state.Changed -= OnStateChanged;
}
