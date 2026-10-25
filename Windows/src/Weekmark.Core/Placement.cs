namespace Weekmark.Core;

public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public bool Contains(double px, double py) => px >= X && px < Right && py >= Y && py < Bottom;
}

public sealed record ScreenInfo(string Id, RectD Bounds, RectD WorkArea, bool IsPrimary);

public sealed record SavedPlacement(string ScreenId, double OffsetX, double OffsetTop);

public enum Corner { TopLeft, TopRight, BottomLeft, BottomRight }

/// <summary>Pure placement maths in physical pixels. The screen whose bounds contain the widget's centre owns it.</summary>
public static class PlacementMath
{
    public const double Margin = 16;

    public static (double X, double Y) Restore(SavedPlacement? saved, IReadOnlyList<ScreenInfo> screens,
        double width, double height)
    {
        if (screens.Count == 0) return (0, 0);
        var valid = saved is not null && double.IsFinite(saved.OffsetX) && double.IsFinite(saved.OffsetTop);
        var match = !valid ? null : screens.FirstOrDefault(s => s.Id == saved!.ScreenId);
        if (match is not null)
            return Clamp(match.Bounds.X + saved!.OffsetX, match.Bounds.Y + saved.OffsetTop, match.WorkArea, width, height);
        var p = Primary(screens);
        var (sx, sy) = Snap(p, Corner.TopRight, width, height);
        return Clamp(sx, sy, p.WorkArea, width, height);
    }

    public static SavedPlacement Capture(double x, double y, double width, double height,
        IReadOnlyList<ScreenInfo> screens)
    {
        if (screens.Count == 0) return new SavedPlacement("", x, y);
        var (cx, cy) = (x + width / 2, y + height / 2);
        var owner = screens.FirstOrDefault(s => s.Bounds.Contains(cx, cy))
            ?? screens.Select(s => (s, a: Overlap(s.Bounds, x, y, width, height))).Where(t => t.a > 0)
                .OrderByDescending(t => t.a).Select(t => t.s).FirstOrDefault()
            ?? Primary(screens);
        return new SavedPlacement(owner.Id, x - owner.Bounds.X, y - owner.Bounds.Y);
    }

    public static (double X, double Y) Snap(ScreenInfo s, Corner c, double width, double height)
    {
        var w = s.WorkArea;
        var x = c is Corner.TopLeft or Corner.BottomLeft ? w.X + Margin : w.Right - width - Margin;
        var y = c is Corner.TopLeft or Corner.TopRight ? w.Y + Margin : w.Bottom - height - Margin;
        return (x, y);
    }

    // Max-then-min order: a widget larger than the area ends up at its top-left.
    private static (double X, double Y) Clamp(double x, double y, RectD area, double width, double height) =>
        (Math.Max(area.X, Math.Min(x, area.Right - width)), Math.Max(area.Y, Math.Min(y, area.Bottom - height)));

    private static ScreenInfo Primary(IReadOnlyList<ScreenInfo> screens) => screens.FirstOrDefault(s => s.IsPrimary) ?? screens[0];

    private static double Overlap(RectD r, double x, double y, double w, double h) =>
        Math.Max(0, Math.Min(r.Right, x + w) - Math.Max(r.X, x)) * Math.Max(0, Math.Min(r.Bottom, y + h) - Math.Max(r.Y, y));
}
