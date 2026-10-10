using NetVips;

namespace WebpResizer;

/// <summary>Resize images and encode them to WebP. All methods are thread-safe.</summary>
/// <remarks>
/// Input can be any format libvips decodes (JPEG, PNG, WebP, GIF, TIFF, HEIF/AVIF and more).
/// EXIF orientation is applied. Resizing uses libvips thumbnail, which shrinks JPEG and WebP during decode.
/// </remarks>
public static class Webp
{
    /// <summary>Resizes encoded image bytes and returns WebP bytes.</summary>
    public static WebpResult Resize(byte[] input, WebpOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        options ??= WebpOptions.Default;
        options.Validate();
        WebpRuntime.EnsureConfigured();

        return Run(() =>
        {
            if (options.MaxInputPixels is { } max)
            {
                using var header = Image.NewFromBuffer(input, access: Enums.Access.Sequential);
                CheckPixels(header, max);
            }
            using var image = Load(input, options);
            return Encode(image, options);
        });
    }

    /// <summary>Resizes an image file and writes a WebP file. Reads from disk incrementally where the format allows.</summary>
    public static WebpResult ResizeFile(string inputPath, string outputPath, WebpOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(inputPath);
        ArgumentException.ThrowIfNullOrEmpty(outputPath);
        options ??= WebpOptions.Default;
        options.Validate();
        WebpRuntime.EnsureConfigured();
        if (!File.Exists(inputPath)) throw new FileNotFoundException("Input image not found.", inputPath);

        var result = Run(() =>
        {
            if (options.MaxInputPixels is { } max)
            {
                using var header = Image.NewFromFile(inputPath, access: Enums.Access.Sequential);
                CheckPixels(header, max);
            }
            using var image = LoadFile(inputPath, options);
            return Encode(image, options);
        });
        File.WriteAllBytes(outputPath, result.Data);
        return result;
    }

    /// <summary>
    /// Reads <paramref name="input"/> to the end, resizes it and writes WebP bytes to <paramref name="output"/>.
    /// I/O is asynchronous; the resize itself runs on the calling thread.
    /// </summary>
    public static async Task<WebpResult> ResizeAsync(Stream input, Stream output, WebpOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        byte[] bytes;
        using (var buffer = new MemoryStream())
        {
            await input.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            bytes = buffer.ToArray();
        }
        cancellationToken.ThrowIfCancellationRequested();
        var result = Resize(bytes, options);
        await output.WriteAsync(result.Data, cancellationToken).ConfigureAwait(false);
        return result;
    }

    static Image Load(byte[] input, WebpOptions o) =>
        IsResize(o)
            ? Image.ThumbnailBuffer(input, TargetWidth(o), height: TargetHeight(o), size: Size(o), crop: Crop(o))
            : LoadUpright(access => Image.NewFromBuffer(input, access: access));

    static Image LoadFile(string path, WebpOptions o) =>
        IsResize(o)
            ? Image.Thumbnail(path, TargetWidth(o), height: TargetHeight(o), size: Size(o), crop: Crop(o))
            : LoadUpright(access => Image.NewFromFile(path, access: access));

    // Sequential access streams the decode, but rotating needs random access, so only pay for it when the EXIF orientation asks.
    static Image LoadUpright(Func<Enums.Access, Image> open)
    {
        var image = open(Enums.Access.Sequential);
        if (!image.Contains("orientation") || (int)image.Get("orientation") <= 1) return image;
        image.Dispose();
        using var random = open(Enums.Access.Random);
        return random.Autorot();
    }

    static WebpResult Encode(Image image, WebpOptions o)
    {
        var data = image.WebpsaveBuffer(
            q: o.Quality,
            lossless: o.Lossless,
            alphaQ: o.AlphaQuality,
            smartSubsample: o.SmartSubsample,
            effort: o.Effort,
            keep: o.KeepMetadata ? Enums.ForeignKeep.All : Enums.ForeignKeep.None);
        return new WebpResult(data, image.Width, image.Height);
    }

    // libvips' largest coordinate: an unbounded side for thumbnail.
    const int Unbounded = 10_000_000;

    static bool IsResize(WebpOptions o) => o.Width is not null || o.Height is not null;
    static int TargetWidth(WebpOptions o) => o.Width ?? Unbounded;
    static int TargetHeight(WebpOptions o) => o.Height ?? Unbounded;
    static Enums.Size Size(WebpOptions o) => o.AllowUpscale ? Enums.Size.Both : Enums.Size.Down;
    static Enums.Interesting Crop(WebpOptions o) => o.Fit switch
    {
        Fit.Cover => Enums.Interesting.Centre,
        Fit.CoverSmart => Enums.Interesting.Attention,
        _ => Enums.Interesting.None,
    };

    static void CheckPixels(Image header, long max)
    {
        long pixels = (long)header.Width * header.Height;
        if (pixels > max)
            throw new ImageProcessingException($"Input is {header.Width}×{header.Height} ({pixels:N0} pixels), above the {max:N0} pixel limit.");
    }

    static WebpResult Run(Func<WebpResult> work)
    {
        try
        {
            return work();
        }
        catch (VipsException e)
        {
            throw new ImageProcessingException($"Image processing failed: {e.Message.Trim()}", e);
        }
    }
}
