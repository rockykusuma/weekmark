using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using Weekmark.App.Platform;
using Weekmark.App.Settings;
using Weekmark.Core;

namespace Weekmark.App.Onboarding;

/// <summary>First run: numbering and holidays guessed from the Windows region, applied on Done. Shown again, it starts from the current settings.</summary>
public sealed partial class WelcomeViewModel : ObservableObject
{
    readonly AppState state;
    readonly string? startupExe;
    readonly int seededRegion;

    public static string[] Numberings => SettingsViewModel.Numberings;
    public static string[] Hotkeys => SettingsViewModel.Hotkeys;
    public static string[] Regions { get; } =
        ["None", .. Enum.GetValues<HolidayRegion>().Select(r => $"{r.Flag()} {r.Title()}")];

    /// <param name="region">Two-letter Windows region, e.g. <c>RegionInfo.CurrentRegion.TwoLetterISORegionName</c>.</param>
    public WelcomeViewModel(AppState state, string region, string? startupExe = null)
    {
        this.state = state;
        this.startupExe = startupExe;
        var s = state.Settings;
        numberingIndex = region.Equals("US", StringComparison.OrdinalIgnoreCase) ? (int)WeekNumbering.Us : (int)WeekNumbering.Iso;
        seededRegion = regionIndex = HolidayRegionExtensions.Guess(region) is { } r ? (int)r + 1 : 0;
        if (!s.OnboardingDone) return;
        // Show Welcome Screen Again: start from the current settings, so Done changes only what the user changes.
        numberingIndex = (int)s.Numbering;
        if (s.Regions.Count > 0) seededRegion = regionIndex = (int)s.Regions[0] + 1;
        startWithWindows = s.StartWithWindows;
        hotkeyIndex = (int)s.Hotkey;
    }

    public static string CurrentRegion()
    {
        try { return RegionInfo.CurrentRegion.TwoLetterISORegionName; }
        catch (ArgumentException) { return ""; } // invariant or unknown region
    }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(WeekTitle), nameof(WeekRange))] private int numberingIndex;
    [ObservableProperty] private int regionIndex;
    [ObservableProperty] private int hotkeyIndex;
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
        if (!s.OnboardingDone) s.Regions = Region is { } r ? [r] : [];
        else if (RegionIndex != seededRegion) // a second visit adds the picked country; None turns holidays off
        {
            if (Region is not { } picked) s.Regions = [];
            else if (!s.Regions.Contains(picked)) s.Regions.Add(picked);
        }
        if (HotkeyIndex is >= 0 and < 5) s.Hotkey = (HotkeyPreset)HotkeyIndex;
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
