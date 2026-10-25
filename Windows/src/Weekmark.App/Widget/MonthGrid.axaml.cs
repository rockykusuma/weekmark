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
}
