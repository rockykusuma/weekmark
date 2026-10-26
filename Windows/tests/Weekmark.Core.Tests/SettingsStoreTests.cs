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
    public void AutomaticUpdateChecksDefaultsOn()
    {
        File.WriteAllText(P, """{"size": "large"}""");
        Assert.True(Store.Load().AutomaticUpdateChecks);
        File.WriteAllText(P, """{"automaticUpdateChecks": false}""");
        var s = Store.Load();
        Assert.False(s.AutomaticUpdateChecks);
        Store.Save(s);
        Assert.False(SavedRoot().GetProperty("automaticUpdateChecks").GetBoolean());
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
        Assert.Equal(HotkeyPreset.Off, s.Hotkey);
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
        Assert.Equal(52, s.Sprint.LengthWeeks);
        Assert.Equal(0, s.Sprint.SprintsPerPI);
        Assert.Equal(12, s.Quarter.FiscalStartMonth);
    }

    [Theory]
    [InlineData(300, 52)]
    [InlineData(53, 52)]
    [InlineData(0, 1)]
    [InlineData(6, 6)]
    public void SprintLengthIsNormalisedToWhatSprintsAccept(int stored, int loaded)
    {
        File.WriteAllText(P, $"{{\"sprint\":{{\"enabled\":true,\"lengthWeeks\":{stored},\"anchor\":{{\"y\":2026,\"m\":1,\"d\":5}}}}}}");
        var s = Store.Load();
        Assert.Equal(loaded, s.Sprint.LengthWeeks);
        Assert.NotNull(Sprints.Info(new(2026, 10, 25), s.Sprint, new WeekCalendar(WeekNumbering.Iso), HolidayStore.Empty));
    }

    [Fact]
    public void FailedWriteReturnsFalseWithoutThrowingOrLeavingTmp()
    {
        var store = Store;
        var s = store.Load();
        Directory.CreateDirectory(P); // something in the way of the final move, on every OS
        s.Size = WidgetSize.Large;
        var saved = 0;
        store.Saved += _ => saved++;

        Assert.False(store.Save(s));
        Assert.NotNull(store.LastSaveError);
        Assert.False(File.Exists(P + ".tmp"));
        Assert.Equal(0, saved);

        Directory.Delete(P);
        Assert.True(store.Save(s));
        Assert.Null(store.LastSaveError);
        Assert.Equal(WidgetSize.Large, Store.Load().Size);
    }

    [Fact]
    public void PartialPlanningObjectsKeepDefaultsForMissingKeys()
    {
        File.WriteAllText(P, """{"sprint":{"enabled":true},"quarter":{"mode":"weeks"}}""");
        var s = Store.Load();
        Assert.Equal(new SprintConfig { Enabled = true }, s.Sprint);
        Assert.Equal(new QuarterConfig { Mode = QuarterMode.Weeks }, s.Quarter);
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

    const string M1 = """{"id":"6F9619FF-8B86-D011-B42D-00C04FC964FF","name":"A","week":{"year":2026,"week":48},"color":"red"}""";
    const string M2 = """{"id":"7F9619FF-8B86-D011-B42D-00C04FC964FF","name":"B","week":{"year":2026,"week":49},"color":"blue"}""";
    const string MBad = """{"id":"8F9619FF-8B86-D011-B42D-00C04FC964FF","name":"C","week":{"year":2026,"week":50},"color":"chartreuse"}""";

    JsonElement SavedRoot()
    {
        using var d = JsonDocument.Parse(File.ReadAllText(P));
        return d.RootElement.Clone();
    }

    [Fact]
    public void BadMilestoneItemIsPreservedOnSave()
    {
        File.WriteAllText(P, $$"""{"milestones":[{{M1}},{{MBad}},{{M2}}]}""");
        var s = Store.Load();
        Assert.Equal(2, s.Milestones.Count);
        Store.Save(s);
        var arr = SavedRoot().GetProperty("milestones");
        Assert.Equal(3, arr.GetArrayLength());
        Assert.Contains(arr.EnumerateArray(), e => e.GetProperty("color").GetString() == "chartreuse" && e.GetProperty("name").GetString() == "C");
        Assert.Equal(2, Store.Load().Milestones.Count);
    }

    [Fact]
    public void UnknownRegionIsPreservedOnSave()
    {
        File.WriteAllText(P, """{"regions":["us","xx","de"]}""");
        var s = Store.Load();
        Assert.Equal([HolidayRegion.Us, HolidayRegion.De], s.Regions);
        Store.Save(s);
        Assert.Equal("us,de,xx", string.Join(",", SavedRoot().GetProperty("regions").EnumerateArray().Select(e => e.GetString())));
    }

    [Fact]
    public void UnreadableSprintIsKeptUntilTheAppReplacesIt()
    {
        File.WriteAllText(P, """{"sprint":{"enabled":true,"lengthWeeks":99999999999,"name":"Keep"}}""");
        var store = Store;
        var s = store.Load();
        Assert.False(s.Sprint.Enabled);
        store.Save(s);
        Assert.Equal(99999999999, SavedRoot().GetProperty("sprint").GetProperty("lengthWeeks").GetInt64());
        Assert.Equal("Keep", SavedRoot().GetProperty("sprint").GetProperty("name").GetString());

        s.Sprint = new SprintConfig { Enabled = true, Name = "New" };
        store.Save(s);
        Assert.Equal("New", SavedRoot().GetProperty("sprint").GetProperty("name").GetString());
    }

    [Fact]
    public void NullsNeverReachConsumers()
    {
        File.WriteAllText(P, $$$"""
        {"milestones":[null,{{{M1}}}],"regions":[null,"us"],"customHolidays":[null],
         "sprint":{"enabled":true,"anchor":null}}
        """);
        var s = Store.Load();
        Assert.DoesNotContain(null!, s.Milestones);
        Assert.Single(s.Milestones);
        Assert.Equal([HolidayRegion.Us], s.Regions);
        Assert.Empty(s.CustomHolidays);
        Assert.Equal(new SprintConfig(), s.Sprint);
        Store.Save(s);
        Assert.Equal(JsonValueKind.Null, SavedRoot().GetProperty("sprint").GetProperty("anchor").ValueKind);
        Assert.Equal(2, SavedRoot().GetProperty("milestones").GetArrayLength());
    }

    [Fact]
    public void UnreadableFileGivesDefaultsAndIsNeverOverwritten()
    {
        File.WriteAllText(P, """{"size":"large"}""");
        var store = Store;
        AppSettings s;
        using (new FileStream(P, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            s = store.Load();
        Assert.Equal(WidgetSize.Medium, s.Size);
        Assert.False(store.Save(s));
        Assert.Equal("""{"size":"large"}""", File.ReadAllText(P));
        Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
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
    [Fact]
    public void UnknownNestedKeysSurviveAnUnrelatedSave()
    {
        File.WriteAllText(P, """
        {"sprint":{"enabled":true,"lengthWeeks":3,"futureSprintKey":{"a":1}},
         "quarter":{"fiscalStartMonth":4,"futureQuarterKey":true},
         "milestones":[{"id":"6F9619FF-8B86-D011-B42D-00C04FC964FF","name":"Ship","week":{"year":2026,"week":48},"note":"keep me"}],
         "customHolidays":[{"day":{"y":2026,"m":3,"d":2},"name":"Founders","source":"custom","tag":7}]}
        """);
        var store = Store;
        var s = store.Load();
        s.Size = WidgetSize.Large;
        s.Sprint = s.Sprint with { Name = "Renamed" }; // a `with` copy keeps the unknown keys
        store.Save(s);

        var root = SavedRoot();
        Assert.Equal(1, root.GetProperty("sprint").GetProperty("futureSprintKey").GetProperty("a").GetInt32());
        Assert.Equal("Renamed", root.GetProperty("sprint").GetProperty("name").GetString());
        Assert.True(root.GetProperty("quarter").GetProperty("futureQuarterKey").GetBoolean());
        Assert.Equal("keep me", root.GetProperty("milestones")[0].GetProperty("note").GetString());
        Assert.Equal(7, root.GetProperty("customHolidays")[0].GetProperty("tag").GetInt32());
        Assert.Equal("large", root.GetProperty("size").GetString());
    }

    [Fact]
    public void ClampedPlanningNumbersAreWrittenBackUnchangedUntilEdited()
    {
        File.WriteAllText(P, """{"sprint":{"enabled":true,"lengthWeeks":60,"sprintsPerPI":-3},"quarter":{"fiscalStartMonth":99}}""");
        var store = Store;
        var s = store.Load();
        Assert.Equal(52, s.Sprint.LengthWeeks);
        Assert.Equal(12, s.Quarter.FiscalStartMonth);

        s.Size = WidgetSize.Small;
        store.Save(s);
        Assert.Equal(60, SavedRoot().GetProperty("sprint").GetProperty("lengthWeeks").GetInt32());
        Assert.Equal(-3, SavedRoot().GetProperty("sprint").GetProperty("sprintsPerPI").GetInt32());
        Assert.Equal(99, SavedRoot().GetProperty("quarter").GetProperty("fiscalStartMonth").GetInt32());

        s.Sprint = s.Sprint with { LengthWeeks = 4 }; // the user edits the sprint: the new value is saved
        store.Save(s);
        Assert.Equal(4, SavedRoot().GetProperty("sprint").GetProperty("lengthWeeks").GetInt32());
        Assert.Equal(99, SavedRoot().GetProperty("quarter").GetProperty("fiscalStartMonth").GetInt32());
    }

    [Fact]
    public void YearWindowRoundTrips()
    {
        var s = Store.Load();
        s.YearWindow = new WindowFrame(100, 80, 1060, 820);
        Assert.True(Store.Save(s));
        Assert.Equal(new WindowFrame(100, 80, 1060, 820), Store.Load().YearWindow);
        using var doc = JsonDocument.Parse(File.ReadAllText(P));
        var y = doc.RootElement.GetProperty("yearWindow");
        foreach (var k in new[] { "x", "y", "width", "height" }) Assert.True(y.TryGetProperty(k, out _));
        Assert.Contains("\"yearWindow\": {", File.ReadAllText(P));
    }

    [Fact]
    public void UnreadableYearWindowIsKeptOnSave()
    {
        File.WriteAllText(P, "{\"yearWindow\": \"oops\"}");
        var s = Store.Load();
        Assert.Null(s.YearWindow);
        Assert.True(Store.Save(s));
        using var doc = JsonDocument.Parse(File.ReadAllText(P));
        Assert.Equal("oops", doc.RootElement.GetProperty("yearWindow").GetString());
    }
}
