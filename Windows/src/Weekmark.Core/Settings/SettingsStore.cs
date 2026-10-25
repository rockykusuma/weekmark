using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Weekmark.Core;

public sealed class SettingsStore(string path)
{
    static SettingsJsonContext C => SettingsJsonContext.Default;

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Weekmark", "settings.json");

    public event Action<AppSettings>? Saved;

    /// <summary>Never throws: a missing, empty, corrupt or unreadable file gives defaults (a corrupt one is backed up first).</summary>
    public AppSettings Load()
    {
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return new(); }
        if (string.IsNullOrWhiteSpace(System.Text.Encoding.UTF8.GetString(bytes).Trim('\uFEFF'))) return new();

        try
        {
            using var doc = JsonDocument.Parse(bytes, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            if (doc.RootElement.ValueKind == JsonValueKind.Object) return Parse(doc.RootElement);
        }
        catch (JsonException) { }
        Backup();
        return new();
    }

    void Backup()
    {
        try { File.Copy(path, $"{path}.bak-{DateTime.Now:yyyyMMdd-HHmmss-fff}", overwrite: false); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    // One bad key falls back to that key's default; the others still load.
    static T Get<T>(JsonElement e, JsonTypeInfo<T> info, T fallback)
    {
        try { return e.Deserialize(info) ?? fallback; }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException or FormatException)
        { return fallback; }
    }

    static AppSettings Parse(JsonElement root)
    {
        var s = new AppSettings();
        foreach (var p in root.EnumerateObject())
        {
            var v = p.Value;
            switch (p.Name)
            {
                case "numbering": s.Numbering = Get(v, C.WeekNumbering, s.Numbering); break;
                case "size": s.Size = Get(v, C.WidgetSize, s.Size); break;
                case "layer": s.Layer = Get(v, C.WindowLayer, s.Layer); break;
                case "background": s.Background = Get(v, C.BackgroundStyle, s.Background); break;
                case "appearance": s.Appearance = Get(v, C.AppearanceMode, s.Appearance); break;
                case "showMonth": s.ShowMonth = Get(v, C.Boolean, s.ShowMonth); break;
                case "showProgress": s.ShowProgress = Get(v, C.Boolean, s.ShowProgress); break;
                case "showPlanning": s.ShowPlanning = Get(v, C.Boolean, s.ShowPlanning); break;
                case "showMilestones": s.ShowMilestones = Get(v, C.Boolean, s.ShowMilestones); break;
                case "showInMenuBar": s.ShowInMenuBar = Get(v, C.Boolean, s.ShowInMenuBar); break;
                case "locked": s.Locked = Get(v, C.Boolean, s.Locked); break;
                case "onboardingDone": s.OnboardingDone = Get(v, C.Boolean, s.OnboardingDone); break;
                case "startWithWindows": s.StartWithWindows = Get(v, C.Boolean, s.StartWithWindows); break;
                case "sprint":
                    var sp = Get(v, C.SprintConfig, s.Sprint);
                    // Absurd values would overflow or stall the working-day loops.
                    s.Sprint = sp with { LengthWeeks = Math.Clamp(sp.LengthWeeks, 1, 520), SprintsPerPI = Math.Clamp(sp.SprintsPerPI, 0, 100) };
                    break;
                case "quarter":
                    var q = Get(v, C.QuarterConfig, s.Quarter);
                    s.Quarter = q with { FiscalStartMonth = Math.Clamp(q.FiscalStartMonth, 1, 12) };
                    break;
                case "milestones": s.Milestones = Get(v, C.ListMilestone, s.Milestones); break;
                case "regions": s.Regions = Get(v, C.ListHolidayRegion, s.Regions).Distinct().ToList(); break;
                case "customHolidays": s.CustomHolidays = Get(v, C.ListHoliday, s.CustomHolidays); break;
                case "screenID": s.ScreenID = Get(v, C.String, null!); break;
                case "offsetX": s.OffsetX = v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var x) ? x : null; break;
                case "offsetTop": s.OffsetTop = v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var t) ? t : null; break;
                default: s.Unknown[p.Name] = v.Clone(); break;
            }
        }
        return s;
    }

    public void Save(AppSettings s)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(s, C.AppSettings));
        if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
        Saved?.Invoke(s);
    }
}
