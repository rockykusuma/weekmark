using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Weekmark.App.Widget;

public partial class WidgetView : UserControl
{
    WidgetViewModel? vm;

    public WidgetView() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (vm is not null) vm.PropertyChanged -= OnVmChanged;
        vm = DataContext as WidgetViewModel;
        if (vm is not null) vm.PropertyChanged += OnVmChanged;
        ApplyScale();
    }

    void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WidgetViewModel.Scale)) ApplyScale();
    }

    void ApplyScale() => Scaler.LayoutTransform = new ScaleTransform(vm?.Scale ?? 1, vm?.Scale ?? 1);

    // Milestone rows take the click (no window drag) and pin their week.
    void OnMilestonePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: MilestoneModel m } c || !e.GetCurrentPoint(c).Properties.IsLeftButtonPressed) return;
        e.Handled = true;
        if (m.CanPin) vm?.Pin(m.Week);
    }

    // A click on the field's padding (the text box handles its own) focuses the field instead of dragging the widget.
    void OnFieldPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        Field.Focus();
    }
}
