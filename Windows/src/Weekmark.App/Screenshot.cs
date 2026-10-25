using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace Weekmark.App;

static class Screenshot
{
    /// <summary>Renders a laid-out control to PNG at 100 % (one pixel per DIP).</summary>
    // ponytail: 1x only; on macOS a 192-dpi RenderTargetBitmap drew the content at 4x, so HiDPI output waits for a need.
    public static void Save(Control c, string path)
    {
        var size = new PixelSize(Math.Max(1, (int)Math.Ceiling(c.Bounds.Width)), Math.Max(1, (int)Math.Ceiling(c.Bounds.Height)));
        using var bmp = new RenderTargetBitmap(size, new Vector(96, 96));
        bmp.Render(c);
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        bmp.Save(path);
    }
}
