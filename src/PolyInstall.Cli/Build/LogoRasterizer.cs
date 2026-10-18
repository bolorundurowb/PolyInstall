using SkiaSharp;
using Svg.Skia;

namespace PolyInstall.Cli.Build;

/// <summary>
/// Converts SVG logos to PNG at build time so the runtime stub only needs to decode raster images.
/// </summary>
internal static class LogoRasterizer
{
    public static bool IsSvg(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && Path.GetExtension(path.Trim()).Equals(".svg", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns <paramref name="logoPath"/> with its extension replaced by <c>.png</c>.</summary>
    public static string ToPngPath(string logoPath) => Path.ChangeExtension(logoPath.Trim(), ".png");

    /// <summary>Renders <paramref name="svgPath"/> to a PNG file at <paramref name="pngPath"/>.</summary>
    /// <exception cref="InvalidOperationException">The SVG could not be loaded, rendered or encoded.</exception>
    public static void RasterizeToPng(string svgPath, string pngPath)
    {
        try
        {
            using var input = File.OpenRead(svgPath);
            var svg = new SKSvg();
            svg.Load(input);
            var picture = svg.Picture
                          ?? throw new InvalidOperationException("The SVG file did not produce a drawable picture.");

            var bounds = picture.CullRect;
            var width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
            var height = Math.Max(1, (int)Math.Ceiling(bounds.Height));
            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            canvas.Translate(-bounds.Left, -bounds.Top);
            canvas.DrawPicture(picture);
            canvas.Flush();

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100)
                             ?? throw new InvalidOperationException("PNG encoding failed.");
            using var output = File.Create(pngPath);
            data.SaveTo(output);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to convert SVG logo '{svgPath}' to PNG: {ex.Message}", ex);
        }
    }
}
