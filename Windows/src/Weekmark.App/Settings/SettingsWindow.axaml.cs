using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Weekmark.App.Settings;

public partial class SettingsWindow : Window
{
    public static readonly string[] TabNames = ["general", "widget", "planning", "holidays", "milestones", "about"];

    public SettingsWindow() => InitializeComponent(); // designer

    public SettingsWindow(SettingsViewModel vm, string? tab = null)
    {
        InitializeComponent();
        DataContext = vm;
        Tabs.SelectedIndex = Math.Max(0, Array.IndexOf(TabNames, tab?.ToLowerInvariant()));
        Closed += (_, _) => vm.Dispose();
    }

    SettingsViewModel Vm => (SettingsViewModel)DataContext!;

    // Never throws: a failed pick or read becomes a line under the button.
    async void ImportIcs(object? sender, RoutedEventArgs e)
    {
        var lines = new List<string>();
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Holidays",
                AllowMultiple = true,
                FileTypeFilter = [new FilePickerFileType("iCalendar (.ics)") { Patterns = ["*.ics"] }],
            });
            foreach (var f in files)
            {
                var name = Path.GetFileNameWithoutExtension(f.Name);
                try
                {
                    await using var stream = await f.OpenReadAsync();
                    using var reader = new StreamReader(stream);
                    lines.Add(Vm.ImportIcs(await reader.ReadToEndAsync(), name));
                }
                catch (Exception ex) { lines.Add($"Couldn't import {name}: {ex.Message}"); }
            }
        }
        catch (Exception ex) { lines.Add($"Couldn't open the file picker: {ex.Message}"); }
        if (lines.Count > 0) Vm.ImportMessage = string.Join("\n", lines);
    }
}
