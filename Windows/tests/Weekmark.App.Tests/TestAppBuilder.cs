using Avalonia;
using Avalonia.Headless;
using Weekmark.App;

[assembly: AvaloniaTestApplication(typeof(Weekmark.App.Tests.TestAppBuilder))]

namespace Weekmark.App.Tests;

public static class TestAppBuilder
{
    // Real Skia drawing (not the headless stub) so rendered frames contain pixels.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
