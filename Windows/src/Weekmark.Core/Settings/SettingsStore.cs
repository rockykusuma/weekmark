using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace Weekmark.Core;

public sealed class SettingsStore(string path)
{
    static SettingsJsonContext C => SettingsJsonContext.Default;

    // Set when the file exists but could not be read: Save must not overwrite what we could not see.
    bool unreadable;

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Weekmark", "settings.json");

    public event Action<AppSettings>? Saved;

    /// <summary>Never throws: a missing, empty, corrupt or unreadable file gives defaults (a corrupt one is backed up first).</summary>
    public AppSettings Load()
    {
        byte[] bytes;
        for (var attempt = 1; ; attempt++)
        {
            try { bytes = File.ReadAllBytes(path); break; }
            catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException) { return new(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Briefly locked by antivirus or a sync client: retry, then give up without ever overwriting the file.
                if (attempt < 3) { Thread.Sleep(100); continue; }
                unreadable = true;
                return new();
            }
        }
        if (string.IsNullOrWhiteSpace(System.Text.Encoding.UTF8.GetString(bytes).Trim('﻿'))) return new();

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

    // One bad key (or list item) falls back to its default; the others still load.
    static bool TryGet<T>(JsonElement e, JsonTypeInfo<T> info, out T value)
    {
        try
        {
            // RespectNullableAnnotations makes nulls in required places throw instead of reaching consumers.
            if (e.Deserialize(info) is { } r) { value = r; return true; }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException or FormatException) { }
        value = default!;
        return false;
    }

    static AppSettings Parse(JsonElement root)
    {
        var s = new AppSettings();
        void Scalar<T>(string key, JsonElement v, JsonTypeInfo<T> info, Action<T> set)
        {
            if (TryGet(v, info, out var r)) set(r); else s.Rejected[key] = new(v.Clone(), null);
        }
        void Items<T>(string key, JsonElement v, JsonTypeInfo<T> info, List<T> target)
        {
            if (v.ValueKind != JsonValueKind.Array) { s.Rejected[key] = new(v.Clone(), null); return; }
            foreach (var item in v.EnumerateArray())
            {
                if (TryGet(item, info, out var r)) target.Add(r);
                else
                {
                    if (!s.BadItems.TryGetValue(key, out var bad)) s.BadItems[key] = bad = [];
                    bad.Add(item.Clone());
                }
            }
        }

        foreach (var p in root.EnumerateObject())
        {
            var v = p.Value;
            switch (p.Name)
            {
                case "numbering": Scalar(p.Name, v, C.WeekNumbering, x => s.Numbering = x); break;
                case "size": Scalar(p.Name, v, C.WidgetSize, x => s.Size = x); break;
                case "layer": Scalar(p.Name, v, C.WindowLayer, x => s.Layer = x); break;
                case "background": Scalar(p.Name, v, C.BackgroundStyle, x => s.Background = x); break;
                case "appearance": Scalar(p.Name, v, C.AppearanceMode, x => s.Appearance = x); break;
                case "showMonth": Scalar(p.Name, v, C.Boolean, x => s.ShowMonth = x); break;
                case "showProgress": Scalar(p.Name, v, C.Boolean, x => s.ShowProgress = x); break;
                case "showPlanning": Scalar(p.Name, v, C.Boolean, x => s.ShowPlanning = x); break;
                case "showMilestones": Scalar(p.Name, v, C.Boolean, x => s.ShowMilestones = x); break;
                case "showInMenuBar": Scalar(p.Name, v, C.Boolean, x => s.ShowInMenuBar = x); break;
                case "locked": Scalar(p.Name, v, C.Boolean, x => s.Locked = x); break;
                case "onboardingDone": Scalar(p.Name, v, C.Boolean, x => s.OnboardingDone = x); break;
                case "startWithWindows": Scalar(p.Name, v, C.Boolean, x => s.StartWithWindows = x); break;
                // Absurd planning numbers would overflow or stall the working-day loops.
                case "sprint":
                    Scalar(p.Name, v, C.SprintConfig, x => s.Sprint = x with { LengthWeeks = Math.Clamp(x.LengthWeeks, 1, 520), SprintsPerPI = Math.Clamp(x.SprintsPerPI, 0, 100) });
                    break;
                case "quarter":
                    Scalar(p.Name, v, C.QuarterConfig, x => s.Quarter = x with { FiscalStartMonth = Math.Clamp(x.FiscalStartMonth, 1, 12) });
                    break;
                case "milestones": Items(p.Name, v, C.Milestone, s.Milestones); break;
                case "regions": Items(p.Name, v, C.HolidayRegion, s.Regions); s.Regions = s.Regions.Distinct().ToList(); break;
                case "customHolidays": Items(p.Name, v, C.Holiday, s.CustomHolidays); break;
                case "screenID": Scalar(p.Name, v, C.String, x => s.ScreenID = x); break;
                case "offsetX": s.OffsetX = v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var x) ? x : null; break;
                case "offsetTop": s.OffsetTop = v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var t) ? t : null; break;
                default: s.Unknown[p.Name] = v.Clone(); break;
            }
        }

        // Remember what we substituted, so Save can tell whether the app has since set a new value.
        if (s.Rejected.Count > 0)
        {
            var node = JsonSerializer.SerializeToNode(s, C.AppSettings)!.AsObject();
            foreach (var k in s.Rejected.Keys.ToList()) s.Rejected[k] = s.Rejected[k] with { Substituted = node[k]?.ToJsonString() };
        }
        return s;
    }

    /// <summary>Returns false (and leaves the file alone) when the existing file could not be read at Load.</summary>
    public bool Save(AppSettings s)
    {
        if (unreadable) return false;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var node = JsonSerializer.SerializeToNode(s, C.AppSettings)!.AsObject();
        // Values we could not read go back unchanged unless the app has replaced them.
        foreach (var (key, r) in s.Rejected)
            if (node[key]?.ToJsonString() == r.Substituted) node[key] = JsonNode.Parse(r.Raw.GetRawText());
        foreach (var (key, items) in s.BadItems)
            if (node[key] is JsonArray arr) foreach (var i in items) arr.Add(JsonNode.Parse(i.GetRawText()));

        var tmp = path + ".tmp";
        File.WriteAllText(tmp, node.ToJsonString(C.Options));
        if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
        Saved?.Invoke(s);
        return true;
    }
}
