using Velopack.Sources;
using Weekmark.App.Updates;

namespace Weekmark.App.Tests;

public sealed class GithubSourceTests
{
    const string Json = """
        [
          {"name":"Mac 1.0","prerelease":false,"published_at":"2026-10-01T10:00:00Z","assets":[{"name":"Weekmark.dmg","url":"https://api.github.com/a/1","browser_download_url":"https://github.com/o/r/releases/download/v1.0/Weekmark.dmg"}]},
          {"name":"Windows 0.3.0","prerelease":false,"published_at":"2026-10-20T10:00:00Z","assets":[{"name":"releases.win-x64.json","url":"https://api.github.com/a/2","browser_download_url":"https://github.com/o/r/releases/download/windows-v0.3.0/releases.win-x64.json"}]}
        ]
        """;

    sealed class Recorder : IFileDownloader
    {
        public List<string> Urls = [];
        public Task<string> DownloadString(string url, IDictionary<string, string>? headers = null, double timeout = 30)
        { Urls.Add(url); return Task.FromResult(Json); }
        public Task DownloadFile(string url, string path, Action<int> progress, IDictionary<string, string>? headers = null, double timeout = 30, CancellationToken cancelToken = default) => Task.CompletedTask;
        public Task<byte[]> DownloadBytes(string url, IDictionary<string, string>? headers = null, double timeout = 30)
        { Urls.Add(url); return Task.FromResult(System.Text.Encoding.UTF8.GetBytes("""{"Assets":[]}""")); }
    }

    [Fact]
    public void ReadsBrowserDownloadUrlNewestFirst()
    {
        var r = WeekmarkGithubSource.Parse(Json, true);
        Assert.Equal("Windows 0.3.0", r[0].Name);
        Assert.Equal("https://github.com/o/r/releases/download/windows-v0.3.0/releases.win-x64.json", r[0].Assets[0].BrowserDownloadUrl);
        Assert.DoesNotContain(r[1].Assets, a => a.Name == "releases.win-x64.json");
    }

    [Fact]
    public async Task AsksForHundredReleases()
    {
        var d = new Recorder();
        await new WeekmarkGithubSource("https://github.com/o/r", null, true, d)
            .GetReleaseFeed(new Velopack.Logging.NullVelopackLogger(), "stable", "win-x64");
        Assert.Contains("per_page=100", d.Urls[0]);
        Assert.EndsWith("releases.win-x64.json", d.Urls[1]); // the asset's browser URL, not the quota-costing API URL
    }
}
