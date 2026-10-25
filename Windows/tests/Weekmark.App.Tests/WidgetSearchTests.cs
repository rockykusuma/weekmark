using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Weekmark.App.Platform;
using Weekmark.App.Widget;
using Weekmark.Core;

namespace Weekmark.App.Tests;

public sealed class WidgetSearchTests
{
    DateTime now = new(2026, 10, 25, 14, 0, 0); // Sunday, CW43

    sealed class RecordingLayering : IWindowLayering
    {
        public List<WindowLayer> Applied { get; } = [];
        public List<bool> Activation { get; } = [];
        /// <summary>For each give-back (allow false): whether the foreground goes back to the previous app.</summary>
        public List<bool> HandBack { get; } = [];
        public void Apply(Window w, WindowLayer layer) { Applied.Add(layer); w.Topmost = layer == WindowLayer.Floating; }
        public void HideFromTaskSwitcher(Window w) { }
        public void AllowActivation(Window w, bool allow, bool handBack = true)
        {
            Activation.Add(allow);
            if (!allow) HandBack.Add(handBack);
        }
    }

    (WidgetWindow W, WidgetViewModel Vm, AppState State, RecordingLayering Layering) Shown(Action<AppSettings>? edit = null)
    {
        CultureInfo.CurrentCulture = new CultureInfo("en-GB");
        var s = DemoData.Create(DateOnly.FromDateTime(now));
        edit?.Invoke(s);
        var state = new AppState(s, null, () => now);
        var layering = new RecordingLayering();
        var w = new WidgetWindow(state, layering);
        w.Show();
        w.UpdateLayout(); // the headless window has its final size only after a layout pass: clicks need it
        return (w, (WidgetViewModel)w.DataContext!, state, layering);
    }

    static TextBox Field(WidgetWindow w) => w.GetVisualDescendants().OfType<TextBox>().Single();

    /// <summary>Clicks the search field the way a user does (headless input, not a programmatic focus).</summary>
    static void ClickField(WidgetWindow w)
    {
        var f = Field(w);
        var p = f.TranslatePoint(new Point(f.Bounds.Width / 2, f.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
    }

    static void Type(WidgetWindow w, WidgetViewModel vm, string text)
    {
        ClickField(w);
        w.KeyTextInput(text);
        vm.FlushSearch(); // the 150 ms debounce
    }

    static void Press(WidgetWindow w, Key key) => w.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);

    [AvaloniaFact]
    public void TypingShowsTheResultAndPinsTheWeek()
    {
        var (w, vm, _, _) = Shown();
        Type(w, vm, "51");
        Assert.Equal("51", vm.Query);
        Assert.StartsWith("CW51 ·", vm.Result!.Title);
        Assert.Equal(new WeekRef(2026, 51), vm.Pinned);
        Assert.Equal("December 2026", vm.MonthTitle);
        Assert.Equal(vm.Result.Title, vm.Details.Title);
        Assert.True(vm.Details.IsPinned); // orange
        Assert.True(vm.IsBrowsing);

        // Hover wins while the pointer is on a week, then the result comes back.
        vm.Hover = new WeekRef(2026, 50);
        Assert.StartsWith("CW50 ·", vm.Details.Title);
        Assert.False(vm.Details.IsPinned);
        vm.Hover = null;
        Assert.Equal(vm.Result.Title, vm.Details.Title);

        // Clicking a week pins it and clears the query, as on the Mac.
        vm.Pin(new WeekRef(2026, 49));
        Assert.Equal("", vm.Query);
        Assert.Null(vm.Result);
        Assert.StartsWith("CW49 ·", vm.Details.Title);
        w.Close();
    }

    [AvaloniaFact]
    public async Task EnterCopiesAndEscClears()
    {
        var (w, vm, _, _) = Shown();
        ClickField(w);
        w.KeyTextInput("51");
        Press(w, Key.Enter); // before the debounce fires: Enter still copies the latest query's result
        Assert.Equal(vm.Result!.CopyText, await w.Clipboard!.TryGetTextAsync());
        Assert.StartsWith("CW51", vm.Result.CopyText);
        Assert.Equal("51", vm.Query); // Enter keeps the query

        ClickField(w);
        Press(w, Key.Escape);
        Assert.Equal("", vm.Query);
        Assert.Null(vm.Result);
        Assert.Null(vm.Pinned);
        Assert.Equal(0, vm.MonthOffset);
        Assert.Equal("October 2026", vm.MonthTitle);
        Assert.True(vm.Details.IsHint);
        w.Close();
    }

    [AvaloniaFact]
    public async Task ErrorShowsNoMatch()
    {
        var (w, vm, _, _) = Shown();
        await w.Clipboard!.SetTextAsync("before");
        Type(w, vm, "hello");
        Assert.Equal(QueryKind.Error, vm.Result!.Kind);
        Assert.Equal("No match for “hello”", vm.Result.Title);
        Assert.Equal("No match for “hello”", vm.Details.Title);
        Assert.True(vm.Details.IsError);
        Assert.Null(vm.Pinned);
        Press(w, Key.Enter);
        Assert.Equal("before", await w.Clipboard.TryGetTextAsync()); // an error copies nothing

        // Deleting the text clears the result at once.
        vm.Query = "";
        Assert.Null(vm.Result);
        Assert.True(vm.Details.IsHint);
        w.Close();
    }

    [AvaloniaFact]
    public void IdleResetsAfterTwoMinutes()
    {
        var (w, vm, state, layering) = Shown();
        Type(w, vm, "51");
        now = now.AddSeconds(90);
        state.Tick();
        Assert.Equal("51", vm.Query);
        Assert.Equal(new WeekRef(2026, 51), vm.Pinned);

        now = now.AddSeconds(31); // 121 s since the last keystroke
        state.Tick();
        Assert.Equal("", vm.Query);
        Assert.Null(vm.Result);
        Assert.Null(vm.Pinned);
        Assert.Equal(0, vm.MonthOffset);
        Assert.Equal([true, false], layering.Activation); // the reset also gives focus back
        Assert.Equal([false], layering.HandBack); // but never hands the foreground to another app
        Assert.False(w.Topmost);
        w.Close();
    }

    [AvaloniaFact]
    public void MidnightWithQueryDoesNotThrow()
    {
        now = new DateTime(2026, 10, 25, 23, 59, 0); // Sunday of CW43
        var (w, vm, state, _) = Shown();
        Type(w, vm, "+1w");
        Assert.Equal(new WeekRef(2026, 44), vm.Pinned);

        now = new DateTime(2026, 10, 26, 0, 0, 30); // Monday of CW44: the relative query moves along
        state.Tick();
        Assert.Equal("44", vm.Header.Week);
        Assert.Equal("+1w", vm.Query);
        Assert.Equal(new WeekRef(2026, 45), vm.Pinned);
        Assert.Equal("+1 week → CW45", vm.Details.Title);

        // Invalid settings values must not throw with a query active either.
        state.Settings.Sprint = state.Settings.Sprint with { LengthWeeks = int.MaxValue / 7 };
        state.Save();
        Assert.Equal("+1w", vm.Query);
        w.Close();
    }

    [AvaloniaFact]
    public void FieldTakesAndReturnsActivation()
    {
        var (w, vm, _, layering) = Shown();
        Assert.Empty(layering.Activation);
        Assert.Equal(WindowLayer.Desktop, layering.Applied[^1]);

        ClickField(w);
        Assert.Equal([true], layering.Activation);
        Assert.True(Field(w).IsFocused);
        Assert.Equal(WindowLayer.Floating, layering.Applied[^1]); // in front while typing

        w.KeyTextInput("46");
        layering.Applied.Clear();
        Press(w, Key.Escape);
        Assert.Equal([true, false], layering.Activation);
        Assert.Equal([true], layering.HandBack); // Esc hands the foreground back
        Assert.Equal([WindowLayer.Desktop], layering.Applied); // back on its layer
        Assert.False(Field(w).IsFocused);
        Assert.False(w.Topmost);
        Assert.Equal("", vm.Query);

        // Clicking away (the widget loses activation) also gives it back, and keeps the query.
        ClickField(w);
        w.KeyTextInput("46");
        var impl = w.PlatformImpl!;
        ((Action)impl.GetType().GetProperty("Deactivated")!.GetValue(impl)!)();
        Assert.Equal([true, false, true, false], layering.Activation);
        Assert.Equal(WindowLayer.Desktop, layering.Applied[^1]);
        Assert.Equal("46", vm.Query);

        ClickField(w);
        Press(w, Key.Enter);
        Assert.Equal([true, true, true], layering.HandBack); // so does Enter
        w.Close();
    }

    /// <summary>Windows may refuse the foreground (foreground lock) or the layering may be broken: no Deactivated comes.</summary>
    [AvaloniaFact]
    public void FieldWithoutActivationStopsFloating()
    {
        var (w, _, _, layering) = Shown();
        ClickField(w);
        Assert.True(w.Topmost);
        Assert.True(w.FloatPending);
        typeof(WindowBase).GetProperty(nameof(WindowBase.IsActive))!.SetValue(w, false); // headless Activate() always succeeds
        layering.Applied.Clear();
        w.FloatTimeout(); // floatEnd's 8 s tick
        Assert.Equal([true, false], layering.Activation);
        Assert.False(w.Topmost);
        Assert.Equal([WindowLayer.Desktop], layering.Applied);
        Assert.False(Field(w).IsFocused);
        Assert.False(w.FloatPending);
        w.Close();
    }

    [AvaloniaFact]
    public void FocusMovingOnStaysWhereItWent()
    {
        var (w, _, _, layering) = Shown();
        ClickField(w);
        var other = w.GetVisualDescendants().OfType<Button>().First(b => b.Focusable && b.IsEffectivelyVisible);
        Assert.True(other.Focus());
        Assert.Equal([true, false], layering.Activation);
        Assert.True(other.IsFocused); // the field's LostFocus doesn't clear the new focus
        w.Close();
    }

    [AvaloniaFact]
    public void WidgetRendersWithSearchField()
    {
        var dir = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "renders")).FullName;
        foreach (var size in Enum.GetValues<WidgetSize>())
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                foreach (var (query, name) in new[] { ("", "empty"), ("51", "week"), ("hello", "error") })
                {
                    var (w, vm, _, _) = Shown(s => { s.Size = size; s.Appearance = theme == ThemeVariant.Dark ? AppearanceMode.Dark : AppearanceMode.Light; });
                    vm.Query = query;
                    vm.FlushSearch();
                    var frame = w.CaptureRenderedFrame();
                    Assert.NotNull(frame);
                    frame.Save(Path.Combine(dir, $"search-{size}-{theme}-{name}.png"));
                    var expected = 300 * size.Scale() + 24;
                    Assert.InRange(frame.PixelSize.Width, expected - 2, expected + 2);
                    Assert.True(Field(w).IsEffectivelyVisible);
                    w.Close();
                }

        // Hidden sections behave as before; the field stays.
        var (off, _, _, _) = Shown(s => s.ShowMonth = false);
        Assert.DoesNotContain(off.GetVisualDescendants().OfType<MonthGrid>(), m => m.IsEffectivelyVisible);
        Assert.True(Field(off).IsEffectivelyVisible);
        off.Close();
    }
}
