using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using Weekmark.App.Platform;
using Weekmark.App.Settings;
using Weekmark.Core;

namespace Weekmark.App.Onboarding;

/// <summary>First run: numbering and holidays guessed from the Windows region, applied on Done.</summary>
public sealed partial class WelcomeViewModel : ObservableObject
{
    readonly AppState state;
    readonly string? startupExe;

    public static string[] Numberings => SettingsViewModel.Numberings;
    public static string[] Regions { get; } =
        ["None", .. Enum.GetValues<HolidayRegion>().Select(r => $"{r.Flag()} {r.Title()}")];

    /// <param name="region">Two-letter Windows region, e.g. <c>RegionInfo.CurrentRegion.TwoLetterISORegionName</c>.</param>
    public WelcomeViewModel(AppState state, string region, string? startupExe = null)
    {
        this.state = state;
        this.startupExe = startupExe;
        numberingIndex = region.Equals("US", StringComparison.OrdinalIgnoreCase) ? (int)WeekNumbering.Us : (int)WeekNumbering.Iso;
        regionIndex = HolidayRegionExtensions.Guess(region) is { } r ? (int)r + 1 : 0;
    }

    public static string CurrentRegion()
    {
        try { return RegionInfo.CurrentRegion.TwoLetterISORegionName; }
        catch (ArgumentException) { return ""; } // invariant or unknown region
    }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(WeekTitle), nameof(WeekRange))] private int numberingIndex;
    [ObservableProperty] private int regionIndex;
    [ObservableProperty] private bool startWithWindows = true;

    public WeekNumbering Numbering => (WeekNumbering)Math.Clamp(NumberingIndex, 0, 1);
    public HolidayRegion? Region => RegionIndex > 0 ? (HolidayRegion)(RegionIndex - 1) : null;

    WeekCalendar Cal => new(Numbering);
    WeekRef Current => Cal.WeekOf(state.Context.Today);
    public string WeekTitle => $"This is CW{Current.Week}";
    public string WeekRange => $"{new WeekFormatter(Cal, WeekFormatter.DayFirstFor(CultureInfo.CurrentCulture)).ShortRange(Current)}, {Current.Year}";

    public void Finish()
    {
        var s = state.Settings;
        s.Numbering = Numbering;
        s.Regions = Region is { } r ? [r] : [];
        s.StartWithWindows = StartWithWindows;
        s.OnboardingDone = true;
        state.Save();
        if (startupExe is not null) Startup.Apply(StartWithWindows, startupExe);
    }
}

public partial class WelcomeWindow : Window
{
    public WelcomeWindow() => InitializeComponent(); // designer

    public WelcomeWindow(WelcomeViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    void Done(object? sender, RoutedEventArgs e)
    {
        ((WelcomeViewModel)DataContext!).Finish();
        Close();
    }
}
