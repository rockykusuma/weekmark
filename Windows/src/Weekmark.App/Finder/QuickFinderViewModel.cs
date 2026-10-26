using CommunityToolkit.Mvvm.ComponentModel;
using Weekmark.Core;

namespace Weekmark.App.Finder;

public sealed partial class QuickFinderViewModel : ObservableObject
{
    public const string Placeholder = "46 · Dec 12 · +6w · 46-50 · until CW52 · milestone";

    readonly AppState state;

    [ObservableProperty] string query = "";
    [ObservableProperty] IReadOnlyList<QueryResult> results = [];
    [ObservableProperty] int selection;
    [ObservableProperty] string? copied;

    public QuickFinderViewModel(AppState state)
    {
        this.state = state;
        Refresh();
    }

    public QueryResult? Selected => Selection >= 0 && Selection < Results.Count ? Results[Selection] : null;

    public string Badge => $"CW{state.Context.Current.Week}";

    partial void OnQueryChanged(string value)
    {
        Selection = 0;
        Copied = null;
        Refresh();
    }

    partial void OnSelectionChanged(int value) => OnPropertyChanged(nameof(Selected));

    public void Move(int delta)
    {
        if (Results.Count > 0) Selection = (Selection + delta + Results.Count) % Results.Count;
    }

    /// <summary>Re-evaluates the query (the clock may have moved on since the finder was last shown).</summary>
    public void Refresh()
    {
        Results = QueryEngine.Evaluate(Query, state.Context);
        Selection = Math.Min(Selection, Math.Max(0, Results.Count - 1));
        OnPropertyChanged(nameof(Selected));
        OnPropertyChanged(nameof(Badge));
    }
}
