using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Weekmark.App.Widget;
using Weekmark.Core;

namespace Weekmark.App.Year;

public sealed record MonthCardModel(string Title, bool IsCurrentMonth, IReadOnlyList<MonthRowModel> Rows);

/// <summary>Year at a Glance: twelve month cards for one year, one pinned week, details for hover ?? pin ?? this week.</summary>
public sealed partial class YearViewModel : ObservableObject, IWeekPicker, IDisposable
{
    readonly AppState state;
    WeekSnapshot snap = null!;
    (DateOnly, int, int)? key;
    WeekNumbering? numbering;
    int year;
    WeekRef? hover;

    public YearViewModel(AppState state)
    {
        this.state = state;
        year = Ctx.Today.Year;
        state.Changed += Refresh;
        Refresh();
    }

    WeekContext Ctx => state.Context;

    [ObservableProperty] private string yearText = "";
    [ObservableProperty] private string subtitle = "";
    [ObservableProperty] private bool showThisYear;
    [ObservableProperty] private IReadOnlyList<string> weekdays = [];
    [ObservableProperty] private IReadOnlyList<MonthCardModel> months = [];
    [ObservableProperty] private WeekRef? pinned;
    [ObservableProperty] private string detailsTitle = "";
    [ObservableProperty] private string detailsSubtitle = "";
    [ObservableProperty] private bool detailsPinned;
    [ObservableProperty] private bool detailsCurrent;
    [ObservableProperty] private string copyText = "";
    [ObservableProperty] private string copyLabel = "";

    public int Year
    {
        get => year;
        set
        {
            value = Math.Clamp(value, 2, 9998);
            if (value == year) return;
            year = value;
            hover = null;
            OnPropertyChanged(nameof(Hover));
            OnPropertyChanged();
            Refresh();
        }
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

    public void Pin(WeekRef r)
    {
        if (r.Year is < 2 or > 9998 || r.Week < 1 || r.Week > Ctx.Cal.WeeksIn(r.Year)) return;
        Pinned = Pinned == r ? null : r;
        UpdateSelection();
    }

    [RelayCommand] void PreviousYear() => Year--;
    [RelayCommand] void NextYear() => Year++;
    [RelayCommand] void ThisYear() => Year = Ctx.Today.Year;

    /// <summary>Month cards per row for a window of this width (card 220, gap 14, padding 20 a side).</summary>
    public static int ColumnsFor(double width) => Math.Clamp((int)((width - 40 + 14) / (220 + 14)), 1, 12);

    void Refresh()
    {
        snap = new WeekSnapshot(Ctx.Now, Ctx.Cal, Ctx.Fmt);
        var k = (Ctx.Today, state.Version, Year);
        if (k != key)
        {
            key = k;
            Rebuild();
        }
        UpdateSelection();
    }

    void Rebuild()
    {
        if (numbering is { } n && n != Ctx.Cal.Numbering) { Pinned = null; hover = null; OnPropertyChanged(nameof(Hover)); }
        numbering = Ctx.Cal.Numbering;
        var fmt = Ctx.Fmt;
        YearText = Year.ToString();
        ShowThisYear = Year != Ctx.Today.Year;
        var weeks = $"{Ctx.Cal.WeeksIn(Year)} calendar weeks";
        Subtitle = snap.Current.Year == Year ? $"{weeks} · week {snap.Current.Week} now, {snap.WeeksLeft} to go" : weeks;
        Weekdays = snap.Days.Select(d => fmt.Format(d, DateTemplate.Weekday)[..1]).ToList();
        Months = Enumerable.Range(1, 12).Select(m =>
        {
            var month = new DateOnly(Year, m, 1);
            return new MonthCardModel(fmt.Format(month, DateTemplate.MonthLong), month.Year == Ctx.Today.Year && m == Ctx.Today.Month,
                MonthRowModel.Build(snap, month, Ctx, state.Settings.Milestones, state.Settings.Sprint, this, forYear: true));
        }).ToList();
    }

    void UpdateSelection()
    {
        foreach (var c in Months)
            foreach (var r in c.Rows)
            {
                r.IsPinned = r.Week == Pinned;
                r.IsHover = r.Week == hover;
            }
        var shown = hover ?? Pinned ?? snap.Current;
        var d = WeekDetails.For(shown, Ctx);
        DetailsTitle = d.Title;
        DetailsSubtitle = d.Detail is { } x ? $"{d.Subtitle} · {x}" : d.Subtitle;
        DetailsPinned = shown == Pinned;
        DetailsCurrent = shown == snap.Current;
        CopyText = d.CopyText;
        CopyLabel = $"Copy “{d.CopyText}”";
    }

    public void Dispose() => state.Changed -= Refresh;
}
