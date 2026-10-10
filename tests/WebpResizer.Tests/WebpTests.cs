using NetVips;

namespace WebpResizer.Tests;

public class WebpTests
{
    static readonly byte[] Landscape = TestImages.Jpeg(1200, 800);

    [Fact]
    public void Resizes_to_width_keeping_aspect_ratio()
    {
        var r = Webp.Resize(Landscape, new WebpOptions { Width = 400 });

        Assert.True(TestImages.IsWebp(r.Data));
        Assert.Equal((400, 267), (r.Width, r.Height));
        using var decoded = TestImages.Decode(r.Data);
        Assert.Equal((400, 267), (decoded.Width, decoded.Height));
    }

    [Fact]
    public void Resizes_to_height_only()
    {
        var r = Webp.Resize(Landscape, new WebpOptions { Height = 200 });
        Assert.Equal((300, 200), (r.Width, r.Height));
    }

    [Fact]
    public void Inside_fits_within_both_dimensions()
    {
        var r = Webp.Resize(Landscape, new WebpOptions { Width = 600, Height = 300 });
        Assert.Equal((450, 300), (r.Width, r.Height));
    }

    [Theory]
    [InlineData(Fit.Cover)]
    [InlineData(Fit.CoverSmart)]
    public void Cover_fills_the_box_exactly(Fit fit)
    {
        var r = Webp.Resize(Landscape, new WebpOptions { Width = 300, Height = 300, Fit = fit });
        Assert.Equal((300, 300), (r.Width, r.Height));
    }

    [Fact]
    public void Without_dimensions_only_converts()
    {
        var r = Webp.Resize(Landscape);
        Assert.True(TestImages.IsWebp(r.Data));
        Assert.Equal((1200, 800), (r.Width, r.Height));
    }

    [Fact]
    public void Does_not_upscale_by_default()
    {
        var r = Webp.Resize(Landscape, new WebpOptions { Width = 2400 });
        Assert.Equal((1200, 800), (r.Width, r.Height));
    }

    [Fact]
    public void Upscales_when_allowed()
    {
        var r = Webp.Resize(Landscape, new WebpOptions { Width = 2400, AllowUpscale = true });
        Assert.Equal((2400, 1600), (r.Width, r.Height));
    }

    [Fact]
    public void Applies_exif_orientation()
    {
        // Orientation 6 = rotate 90° clockwise, so a landscape file displays as portrait.
        var rotated = TestImages.Jpeg(1200, 800, orientation: 6);

        Assert.Equal((800, 1200), (Webp.Resize(rotated).Width, Webp.Resize(rotated).Height));
        var r = Webp.Resize(rotated, new WebpOptions { Width = 400 });
        Assert.Equal((400, 600), (r.Width, r.Height));
    }

    [Fact]
    public void Strips_metadata_by_default_and_keeps_it_on_request()
    {
        var withExif = TestImages.Jpeg(400, 300, orientation: 1);

        Assert.False(TestImages.HasChunk(Webp.Resize(withExif, new WebpOptions { Width = 200 }).Data, "EXIF"));
        Assert.True(TestImages.HasChunk(Webp.Resize(withExif, new WebpOptions { Width = 200, KeepMetadata = true }).Data, "EXIF"));
    }

    [Fact]
    public void Keeps_alpha()
    {
        var r = Webp.Resize(TestImages.PngWithAlpha(800, 600), new WebpOptions { Width = 400 });
        using var decoded = TestImages.Decode(r.Data);
        Assert.Equal(4, decoded.Bands);
        Assert.True(decoded.HasAlpha());
    }

    [Fact]
    public void Lossless_preserves_pixels()
    {
        var png = TestImages.Png(64, 48);
        var r = Webp.Resize(png, new WebpOptions { Lossless = true });

        using var expected = Image.NewFromBuffer(png);
        using var actual = TestImages.Decode(r.Data);
        using var diff = (expected - actual).Abs();
        Assert.Equal(0, diff.Max());
    }

    [Fact]
    public void Higher_quality_gives_larger_files()
    {
        var low = Webp.Resize(Landscape, new WebpOptions { Width = 600, Quality = 30 });
        var high = Webp.Resize(Landscape, new WebpOptions { Width = 600, Quality = 95 });
        Assert.True(high.Data.Length > low.Data.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void Every_effort_level_produces_valid_webp(int effort)
    {
        var r = Webp.Resize(Landscape, new WebpOptions { Width = 600, Effort = effort });
        Assert.True(TestImages.IsWebp(r.Data));
        using var decoded = TestImages.Decode(r.Data);
        Assert.Equal(600, decoded.Width);
    }

    [Fact]
    public void Accepts_png_and_webp_input()
    {
        var fromPng = Webp.Resize(TestImages.Png(800, 400), new WebpOptions { Width = 200 });
        var fromWebp = Webp.Resize(fromPng.Data, new WebpOptions { Width = 100 });
        Assert.Equal((100, 50), (fromWebp.Width, fromWebp.Height));
    }

    [Fact]
    public void Rejects_inputs_above_the_pixel_limit()
    {
        var e = Assert.Throws<ImageProcessingException>(() =>
            Webp.Resize(Landscape, new WebpOptions { Width = 100, MaxInputPixels = 1200 * 800 - 1 }));
        Assert.Contains("pixel limit", e.Message);

        Webp.Resize(Landscape, new WebpOptions { Width = 100, MaxInputPixels = null });
    }

    [Fact]
    public void Wraps_decode_errors()
    {
        Assert.Throws<ImageProcessingException>(() => Webp.Resize("not an image at all"u8.ToArray(), new WebpOptions { Width = 10 }));
    }

    [Theory]
    [InlineData(0, null, 80, 4)]
    [InlineData(null, -1, 80, 4)]
    [InlineData(100, null, 0, 4)]
    [InlineData(100, null, 101, 4)]
    [InlineData(100, null, 80, 7)]
    [InlineData(100, null, 80, -1)]
    public void Rejects_invalid_options(int? width, int? height, int quality, int effort)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Webp.Resize(Landscape, new WebpOptions { Width = width, Height = height, Quality = quality, Effort = effort }));
    }

    [Fact]
    public void Cover_needs_both_dimensions()
    {
        Assert.Throws<ArgumentException>(() => Webp.Resize(Landscape, new WebpOptions { Width = 100, Fit = Fit.Cover }));
    }

    [Fact]
    public void ResizeFile_writes_webp()
    {
        var dir = Directory.CreateTempSubdirectory("webpresizer-");
        try
        {
            var input = Path.Combine(dir.FullName, "in.jpg");
            var output = Path.Combine(dir.FullName, "out.webp");
            File.WriteAllBytes(input, Landscape);

            var r = Webp.ResizeFile(input, output, new WebpOptions { Width = 300 });

            Assert.Equal((300, 200), (r.Width, r.Height));
            Assert.Equal(r.Data, File.ReadAllBytes(output));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void ResizeFile_applies_exif_orientation_without_resizing()
    {
        var dir = Directory.CreateTempSubdirectory("webpresizer-");
        try
        {
            var input = Path.Combine(dir.FullName, "rotated.jpg");
            File.WriteAllBytes(input, TestImages.Jpeg(600, 400, orientation: 6));

            var r = Webp.ResizeFile(input, Path.Combine(dir.FullName, "out.webp"));

            Assert.Equal((400, 600), (r.Width, r.Height));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void ResizeFile_reports_missing_input()
    {
        Assert.Throws<FileNotFoundException>(() => Webp.ResizeFile("does-not-exist.jpg", "out.webp"));
    }

    [Fact]
    public async Task ResizeAsync_reads_and_writes_streams()
    {
        using var input = new MemoryStream(Landscape);
        using var output = new MemoryStream();

        var r = await Webp.ResizeAsync(input, output, new WebpOptions { Width = 120 });

        Assert.Equal(120, r.Width);
        Assert.Equal(r.Data, output.ToArray());
    }

    [Fact]
    public void Is_safe_to_call_in_parallel()
    {
        var options = new WebpOptions { Width = 256 };
        var results = new WebpResult[64];
        Parallel.For(0, results.Length, i => results[i] = Webp.Resize(Landscape, options));

        Assert.All(results, r => Assert.Equal((256, 171), (r.Width, r.Height)));
        Assert.All(results, r => Assert.Equal(results[0].Data, r.Data));
    }

    [Fact]
    public void Reports_libvips_version()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", WebpRuntime.LibvipsVersion);
    }
}
