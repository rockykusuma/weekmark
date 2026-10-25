using System.Text.Json;
using Weekmark.Core;

namespace Weekmark.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("weekmark-settings").FullName;
    string P => Path.Combine(dir, "settings.json");
    SettingsStore Store => new(P);

    public void Dispose() => Directory.Delete(dir, true);

    [Fact]
    public void MissingFileGivesDefaults()
    {
        var s = Store.Load();
        Assert.Equal(WeekNumbering.Iso, s.Numbering);
        Assert.Equal(WidgetSize.Medium, s.Size);
        Assert.Equal(1.0, s.Size.Scale());
        Assert.True(s.StartWithWindows);
        Assert.False(s.OnboardingDone);
        Assert.Empty(s.Milestones);
        Assert.Null(s.ScreenID);
    }

    [Fact]
    public void EmptyFileGivesDefaults()
    {
        File.WriteAllText(P, "");
        Assert.Equal(WidgetSize.Medium, Store.Load().Size);
        Assert.Empty(Directory.GetFiles(dir, "*.bak-*"));
    }

    [Fact]
    public void CorruptFileGivesDefaultsAndBackup()
    {
        File.WriteAllText(P, "{ not json");
        Assert.Equal(WidgetSize.Medium, Store.Load().Size);
        var bak = Assert.Single(Directory.GetFiles(dir, "settings.json.bak-*"));
        Assert.Equal("{ not json", File.ReadAllText(bak));
    }

    [Fact]
    public void NonObjectJsonGivesDefaultsAndBackup()
    {
        File.WriteAllText(P, "[1,2]");
        Assert.Equal(WidgetSize.Medium, Store.Load().Size);
        Assert.Single(Directory.GetFiles(dir, "settings.json.bak-*"));
    }

    [Fact]
    public void RoundTripKeepsEverything()
    {
        var id = Guid.NewGuid();
        var s = new AppSettings
        {
            Numbering = WeekNumbering.Us, Size = WidgetSize.Xlarge, Layer = WindowLayer.Floating,
            Background = BackgroundStyle.Clear, Appearance = AppearanceMode.Dark, ShowMonth = false, Locked = true,
            OnboardingDone = true, StartWithWindows = false,
            Sprint = new SprintConfig { Enabled = true, LengthWeeks = 3, Name = "Iter" },
            Quarter = new QuarterConfig { Mode = QuarterMode.Weeks, FiscalStartMonth = 4 },
            Milestones = [new Milestone(id, "Launch", new WeekRef(2026, 50), MilestoneColor.Teal)],
            Regions = [HolidayRegion.Gb, HolidayRegion.De],
            CustomHolidays = [new Holiday(new Day(2026, 12, 24), "Xmas Eve", "custom")],
            ScreenID = "\\\\.\\DISPLAY2", OffsetX = -12.5, OffsetTop = 40,
        };
        Store.Save(s);
        var r = Store.Load();
        Assert.Equal(WeekNumbering.Us, r.Numbering);
        Assert.Equal(WidgetSize.Xlarge, r.Size);
        Assert.Equal(WindowLayer.Floating, r.Layer);
        Assert.Equal(BackgroundStyle.Clear, r.Background);
        Assert.Equal(AppearanceMode.Dark, r.Appearance);
        Assert.False(r.ShowMonth);
        Assert.True(r.Locked);
        Assert.True(r.OnboardingDone);
        Assert.False(r.StartWithWindows);
        Assert.Equal(s.Sprint, r.Sprint);
        Assert.Equal(s.Quarter, r.Quarter);
        Assert.Equal(s.Milestones, r.Milestones);
        Assert.Equal(s.Regions, r.Regions);
        Assert.Equal(s.CustomHolidays, r.CustomHolidays);
        Assert.Equal("\\\\.\\DISPLAY2", r.ScreenID);
        Assert.Equal(-12.5, r.OffsetX);
        Assert.Equal(40, r.OffsetTop);
        Assert.Contains("\"size\": \"xlarge\"", File.ReadAllText(P));
    }

    [Fact]
    public void UnknownKeysSurviveSave()
    {
        File.WriteAllText(P, """{"futureKey": {"a": 1}, "size": "large"}""");
        Store.Save(Store.Load());
        using var doc = JsonDocument.Parse(File.ReadAllText(P));
        Assert.Equal(1, doc.RootElement.GetProperty("futureKey").GetProperty("a").GetInt32());
        Assert.Equal("large", doc.RootElement.GetProperty("size").GetString());
    }

    [Fact]
    public void MacValuesAccepted()
    {
        File.WriteAllText(P, """
        {"numbering":"system","hotkey":"off",
         "sprint":{"enabled":true,"lengthWeeks":2,"anchor":{"y":2026,"m":1,"d":5},"firstNumber":4,"name":"Sprint","sprintsPerPI":5,"firstPI":2},
         "quarter":{"enabled":true,"fiscalStartMonth":7,"mode":"months"},
         "milestones":[{"id":"6F9619FF-8B86-D011-B42D-00C04FC964FF","name":"Ship","week":{"year":2026,"week":48},"color":"purple"}],
         "regions":["us","uk"],
         "customHolidays":[{"day":{"y":2026,"m":3,"d":2},"name":"Founders","source":"custom"}]}
        """);
        var s = Store.Load();
        Assert.Equal(WeekNumbering.Iso, s.Numbering);
        Assert.Equal(4, s.Sprint.FirstNumber);
        Assert.Equal(5, s.Sprint.SprintsPerPI);
        Assert.Equal(7, s.Quarter.FiscalStartMonth);
        Assert.Equal(MilestoneColor.Purple, Assert.Single(s.Milestones).Color);
        Assert.Equal([HolidayRegion.Us, HolidayRegion.Gb], s.Regions);
        Assert.Equal("Founders", Assert.Single(s.CustomHolidays).Name);
        Assert.True(s.Unknown.ContainsKey("hotkey"));
    }

    [Fact]
    public void OneBadKeyKeepsTheOthers()
    {
        File.WriteAllText(P, """
        {"size":"xlarge","numbering":5,"layer":"sideways","showMonth":"yes","quarter":{"mode":"fortnights"},
         "milestones":[{"id":"6F9619FF-8B86-D011-B42D-00C04FC964FF","name":"A","week":{"year":2026,"week":48},"color":"chartreuse"}],
         "regions":["us"],"sprint":"nope","offsetX":"left","screenID":7}
        """);
        var s = Store.Load();
        Assert.Equal(WidgetSize.Xlarge, s.Size);
        Assert.Equal([HolidayRegion.Us], s.Regions);
        Assert.Equal(WeekNumbering.Iso, s.Numbering);
        Assert.Equal(WindowLayer.Desktop, s.Layer);
        Assert.True(s.ShowMonth);
        Assert.Equal(QuarterMode.Months, s.Quarter.Mode);
        Assert.Empty(s.Milestones);
        Assert.False(s.Sprint.Enabled);
        Assert.Null(s.OffsetX);
        Assert.Null(s.ScreenID);
        Assert.Empty(Directory.GetFiles(dir, "*.bak-*"));
    }

    [Fact]
    public void AbsurdPlanningNumbersAreClamped()
    {
        File.WriteAllText(P, """
        {"sprint":{"enabled":true,"lengthWeeks":2000000000,"sprintsPerPI":-3},"quarter":{"fiscalStartMonth":99}}
        """);
        var s = Store.Load();
        Assert.Equal(520, s.Sprint.LengthWeeks);
        Assert.Equal(0, s.Sprint.SprintsPerPI);
        Assert.Equal(12, s.Quarter.FiscalStartMonth);
    }

    [Fact]
    public void InvalidDatesInValidObjectsLoadAsIs()
    {
        File.WriteAllText(P, """
        {"sprint":{"enabled":true,"anchor":{"y":2026,"m":13,"d":1}},
         "milestones":[{"id":"6F9619FF-8B86-D011-B42D-00C04FC964FF","name":"X","week":{"year":2026,"week":99},"color":"red"}]}
        """);
        var s = Store.Load();
        Assert.Equal(13, s.Sprint.Anchor.M);
        Assert.Equal(99, Assert.Single(s.Milestones).Week.Week);
    }

    [Fact]
    public void SaveIsAtomic()
    {
        var store = Store;
        var raised = 0;
        store.Saved += _ => raised++;
        store.Save(new AppSettings());
        store.Save(new AppSettings { Size = WidgetSize.Small });
        Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
        Assert.Equal(WidgetSize.Small, store.Load().Size);
        Assert.Equal(2, raised);
    }
}
