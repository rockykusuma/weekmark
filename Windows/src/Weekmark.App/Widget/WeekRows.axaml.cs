using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Weekmark.Core;

namespace Weekmark.App.Widget;

/// <summary>What a row's pointer drives: the widget's browser, or the year window's own selection.</summary>
public interface IWeekPicker
{
    WeekRef? Hover { get; set; }
    void Pin(WeekRef r);
}

public partial class WeekRows : UserControl
{
    public static readonly StyledProperty<IReadOnlyList<MonthRowModel>?> RowsProperty =
        AvaloniaProperty.Register<WeekRows, IReadOnlyList<MonthRowModel>?>(nameof(Rows));

    public WeekRows() => InitializeComponent();

    public IReadOnlyList<MonthRowModel>? Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    static MonthRowModel? Row(object? sender) => sender is Control { DataContext: MonthRowModel { IsSpacer: false } r } ? r : null;

    void OnRowEntered(object? sender, PointerEventArgs e)
    {
        if (Row(sender) is { } r) r.Picker.Hover = r.Week;
    }

    void OnRowExited(object? sender, PointerEventArgs e)
    {
        if (Row(sender) is { } r && r.Picker.Hover == r.Week) r.Picker.Hover = null;
    }

    void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Row(sender) is not { } r || !e.GetCurrentPoint((Control)sender!).Properties.IsLeftButtonPressed) return;
        e.Handled = true; // a click on the calendar pins a week instead of dragging the widget
        r.Picker.Pin(r.Week);
    }
}
