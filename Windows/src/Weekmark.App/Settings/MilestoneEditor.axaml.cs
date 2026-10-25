using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Weekmark.App.Settings;

/// <summary>Add/edit form for <see cref="SettingsViewModel.Draft"/>: name, year + week (dates shown), colour.</summary>
public partial class MilestoneEditor : UserControl
{
    /// <summary>Colour name ("Orange") to the widget's brush for it.</summary>
    public static readonly IValueConverter ColorBrush = new FuncValueConverter<string, IBrush?>(name =>
        Array.IndexOf(SettingsViewModel.Colors, name) is var i and >= 0 ? SettingsViewModel.ColorBrushes[i] : null);

    public MilestoneEditor() => InitializeComponent();
}
