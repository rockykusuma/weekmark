using System.Text.Json;

namespace Weekmark.Core.Tests;

public static class Vectors
{
    public static readonly JsonElement Root = Load();

    static JsonElement Load()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "vectors", "cwcore-v1.json")));
        return doc.RootElement.Clone();
    }
}
