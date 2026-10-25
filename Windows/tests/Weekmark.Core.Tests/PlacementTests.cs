namespace Weekmark.Core.Tests;

public class PlacementTests
{
    private static readonly ScreenInfo Main = new("main", new(0, 0, 1920, 1080), new(0, 0, 1920, 1040), true);
    private static readonly ScreenInfo Left = new("left", new(-2560, 0, 2560, 1440), new(-2560, 0, 2560, 1400), false);
    private static readonly ScreenInfo[] Both = [Main, Left];

    [Fact]
    public void NoSavedGoesTopRightOfPrimary()
    {
        var (x, y) = PlacementMath.Restore(null, Both, 300, 400);
        Assert.Equal((1920 - 300 - 16, 16), (x, y));
    }

    [Fact]
    public void SavedScreenPresentRestoresOffset()
    {
        var (x, y) = PlacementMath.Restore(new("main", 100, 200), Both, 300, 400);
        Assert.Equal((100, 200), (x, y));
    }

    [Fact]
    public void SavedScreenMissingFallsBackToPrimaryFullyVisible()
    {
        var (x, y) = PlacementMath.Restore(new("gone", 5000, 5000), [Main], 300, 400);
        Assert.Equal((1920 - 300 - 16, 16), (x, y));
    }

    [Fact]
    public void NegativeCoordinateScreenLeftOfPrimary()
    {
        var (x, y) = PlacementMath.Restore(new("left", 100, 200), Both, 300, 400);
        Assert.Equal((-2460, 200), (x, y));
    }

    [Fact]
    public void ClampsWhenScreenShrank()
    {
        var small = Main with { Bounds = new(0, 0, 1280, 720), WorkArea = new(0, 0, 1280, 680) };
        var (x, y) = PlacementMath.Restore(new("main", 1800, 900), [small], 300, 400);
        Assert.Equal((980, 280), (x, y));
    }

    [Fact]
    public void WidgetLargerThanWorkAreaAlignsTopLeft()
    {
        var (x, y) = PlacementMath.Restore(new("main", 50, 50), [Main], 3000, 2000);
        Assert.Equal((0, 0), (x, y));
    }

    [Fact]
    public void NoPrimaryUsesFirstAndEmptyReturnsOrigin()
    {
        var (x, _) = PlacementMath.Restore(null, [Left], 300, 400);
        Assert.Equal(-300 - 16, x);
        Assert.Equal((0, 0), PlacementMath.Restore(null, [], 300, 400));
    }

    [Fact]
    public void CaptureThenRestoreRoundTrips()
    {
        var saved = PlacementMath.Capture(-2000, 300, 300, 400, Both);
        Assert.Equal(new SavedPlacement("left", 560, 300), saved);
        Assert.Equal((-2000, 300), PlacementMath.Restore(saved, Both, 300, 400));
    }

    [Fact]
    public void CaptureStraddlingPicksCentreOwner()
    {
        // centre x = -10 + 150 = 140 -> main
        Assert.Equal("main", PlacementMath.Capture(-10, 0, 300, 400, Both).ScreenId);
    }

    [Fact]
    public void CaptureOffEverywhereUsesLargestOverlapThenPrimary()
    {
        var a = new ScreenInfo("a", new(0, 0, 200, 100), new(0, 0, 200, 100), false);
        var b = new ScreenInfo("b", new(300, 0, 100, 100), new(300, 0, 100, 100), true);
        // centre (160,50) is in the gap; widget x -10..330 overlaps a by 200x100, b by 30x100 -> a
        Assert.Equal("a", PlacementMath.Capture(-10, 0, 340, 100, [a, b]).ScreenId);
        Assert.Equal("b", PlacementMath.Capture(1000, 1000, 10, 10, [a, b]).ScreenId);
    }

    [Fact]
    public void FallbackNearWorkAreaWidthStaysInside()
    {
        var (x, _) = PlacementMath.Restore(null, [Main], 1910, 400);
        Assert.Equal(0, x);
    }

    [Fact]
    public void NonFiniteSavedOffsetsAreIgnored()
    {
        var def = PlacementMath.Restore(null, Both, 300, 400);
        Assert.Equal(def, PlacementMath.Restore(new("main", double.NaN, 5), Both, 300, 400));
        Assert.Equal(def, PlacementMath.Restore(new("main", 5, double.PositiveInfinity), Both, 300, 400));
    }

    [Fact]
    public void SnapCorners()
    {
        Assert.Equal((16, 16), PlacementMath.Snap(Main, Corner.TopLeft, 300, 400));
        Assert.Equal((1604, 16), PlacementMath.Snap(Main, Corner.TopRight, 300, 400));
        Assert.Equal((16, 624), PlacementMath.Snap(Main, Corner.BottomLeft, 300, 400));
        Assert.Equal((1604, 624), PlacementMath.Snap(Main, Corner.BottomRight, 300, 400));
    }
}
