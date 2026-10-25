using Avalonia.Controls;
using Avalonia.Input;

namespace Weekmark.App.Widget;

public partial class MonthGrid : UserControl
{
    double wheel;

    public MonthGrid()
    {
        InitializeComponent();
        PointerWheelChanged += OnWheel;
    }

    WidgetViewModel? Vm => DataContext as WidgetViewModel;

    // Touchpads send small fractional deltas: one month per whole notch.
    void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        e.Handled = true;
        wheel += e.Delta.Y;
        if (Math.Abs(wheel) < 1) return;
        Vm?.Step(wheel > 0 ? -1 : 1);
        wheel = 0;
    }

    void OnRowEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Control { DataContext: MonthRowModel r } && Vm is { } vm) vm.Hover = r.Week;
    }

    void OnRowExited(object? sender, PointerEventArgs e)
    {
        if (sender is Control { DataContext: MonthRowModel r } && Vm is { } vm && vm.Hover == r.Week) vm.Hover = null;
    }

    void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: MonthRowModel r } c || !e.GetCurrentPoint(c).Properties.IsLeftButtonPressed) return;
        e.Handled = true; // a click on the calendar pins a week instead of dragging the widget
        Vm?.Pin(r.Week);
    }
}
