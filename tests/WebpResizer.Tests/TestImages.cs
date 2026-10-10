using NetVips;

namespace WebpResizer.Tests;

/// Synthetic inputs generated with libvips, so the repo carries no binary fixtures.
static class TestImages
{
    /// A smooth RGB gradient with some detail, encoded as JPEG.
    public static byte[] Jpeg(int width, int height, int? orientation = null)
    {
        using var img = Rgb(width, height);
        if (orientation is { } o)
        {
            using var tagged = img.Mutate(m => m.Set(GValue.GIntType, "orientation", o));
            return tagged.JpegsaveBuffer(q: 90);
        }
        return img.JpegsaveBuffer(q: 90);
    }

    /// RGBA PNG with a horizontal alpha ramp.
    public static byte[] PngWithAlpha(int width, int height)
    {
        using var rgb = Rgb(width, height);
        using var xyz = Image.Xyz(width, height);
        using var x = xyz[0];
        using var alpha = (x * (255.0 / Math.Max(1, width - 1))).Cast(Enums.BandFormat.Uchar);
        using var rgba = rgb.Bandjoin(alpha);
        return rgba.PngsaveBuffer();
    }

    public static byte[] Png(int width, int height)
    {
        using var rgb = Rgb(width, height);
        return rgb.PngsaveBuffer();
    }

    static Image Rgb(int width, int height)
    {
        using var xyz = Image.Xyz(width, height);
        using var x = xyz[0];
        using var y = xyz[1];
        using var r = x * (255.0 / Math.Max(1, width - 1));
        using var g = y * (255.0 / Math.Max(1, height - 1));
        using var b = ((x + y) % 32) * 8;
        using var joined = r.Bandjoin(g, b);
        return joined.Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);
    }

    public static Image Decode(byte[] webp) => Image.NewFromBuffer(webp);

    public static bool IsWebp(byte[] data) =>
        data.Length > 12
        && data.AsSpan(0, 4).SequenceEqual("RIFF"u8)
        && data.AsSpan(8, 4).SequenceEqual("WEBP"u8);

    public static bool HasChunk(byte[] webp, string fourCc)
    {
        var tag = System.Text.Encoding.ASCII.GetBytes(fourCc);
        return webp.AsSpan(12).IndexOf(tag) >= 0;
    }
}
