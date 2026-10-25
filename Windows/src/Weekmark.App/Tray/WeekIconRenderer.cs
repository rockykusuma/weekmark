using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Weekmark.App.Tray;

public static class WeekIconRenderer
{
    static readonly Typeface Bold = new(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);
    static readonly IBrush OnDark = Brushes.White, OnLight = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1C));

    /// <summary>The week number, bold and as large as fits inside a 1 px transparent border, on a transparent background.</summary>
    public static Bitmap Render(int week, int pixelSize, bool lightTaskbar)
    {
        var text = new FormattedText(week.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, Bold, 100, Brushes.White); // no geometry without a brush
        // Fit the glyph outlines, not the text box: line spacing would waste a third of a 16 px icon.
        var glyphs = text.BuildGeometry(default)!;
        var b = glyphs.Bounds;
        var scale = (pixelSize - 2) / Math.Max(b.Width, b.Height);
        var bitmap = new RenderTargetBitmap(new PixelSize(pixelSize, pixelSize), new Vector(96, 96));
        using (var ctx = bitmap.CreateDrawingContext())
        using (ctx.PushTransform(Matrix.CreateTranslation(-b.Center.X, -b.Center.Y) * Matrix.CreateScale(scale, scale)
                                 * Matrix.CreateTranslation(pixelSize / 2.0, pixelSize / 2.0)))
            ctx.DrawGeometry(lightTaskbar ? OnLight : OnDark, null, glyphs);
        return bitmap;
    }
}
