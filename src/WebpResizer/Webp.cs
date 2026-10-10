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
        options = Prepare(options);

        return Run(() =>
        {
            // A memory source pins the array and lets libvips read it in place, so the input is never copied to native memory.
            using var source = Source.NewFromMemory(input);
            return Process(source, options);
        });
    }

    /// <summary>Resizes an image file and writes a WebP file. Reads from disk incrementally where the format allows.</summary>
    public static WebpResult ResizeFile(string inputPath, string outputPath, WebpOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(inputPath);
        ArgumentException.ThrowIfNullOrEmpty(outputPath);
        options = Prepare(options);
        if (!File.Exists(inputPath)) throw new FileNotFoundException("Input image not found.", inputPath);
        var outputDir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (outputDir is not null && !Directory.Exists(outputDir)) throw new DirectoryNotFoundException($"Output directory not found: {outputDir}");

        var result = Run(() =>
        {
            // A file source takes the path literally; libvips' file loaders would parse a trailing "[...]" as loader options.
            using var source = Source.NewFromFile(inputPath);
            return Process(source, options);
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
        options = Prepare(options);
        byte[] bytes;
        using (var buffer = NewBuffer(input))
        {
            await input.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            // When the length was known up front the buffer is exactly full, so hand it over instead of copying it again.
            bytes = buffer.TryGetBuffer(out var segment) && segment.Count == segment.Array!.Length ? segment.Array : buffer.ToArray();
        }
        cancellationToken.ThrowIfCancellationRequested();
        var result = Resize(bytes, options);
        await output.WriteAsync(result.Data, cancellationToken).ConfigureAwait(false);
        return result;
    }

    // Size the buffer from the stream length when it is known, so a large upload is not grown by doubling.
    static MemoryStream NewBuffer(Stream input)
    {
        if (!input.CanSeek) return new MemoryStream();
        long remaining = input.Length - input.Position;
        return remaining is > 0 and <= int.MaxValue ? new MemoryStream((int)remaining) : new MemoryStream();
    }

    static WebpOptions Prepare(WebpOptions? options)
    {
        options ??= WebpOptions.Default;
        options.Validate();
        WebpRuntime.EnsureConfigured();
        return options;
    }

    // The whole pipeline over one libvips source: the pixel check reads only the header, then the source is rewound for the load.
    static WebpResult Process(Source source, WebpOptions o)
    {
        if (o.MaxInputPixels is { } max)
        {
            using var header = Image.NewFromSource(source, access: Enums.Access.Sequential);
            CheckPixels(header, max);
        }
        using var image = IsResize(o)
            ? Image.ThumbnailSource(source, TargetWidth(o), height: TargetHeight(o), size: Size(o), crop: Crop(o))
            : LoadUpright(access => Image.NewFromSource(source, access: access));
        return Encode(image, o);
    }

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
        if (image.Width > WebpMaxSide || image.Height > WebpMaxSide)
            throw new ImageProcessingException(
                $"Output would be {image.Width}×{image.Height}, but WebP allows at most {WebpMaxSide} pixels per side. Set Width or Height to resize it.");

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

    // The WebP format's limit per side (14 bits).
    const int WebpMaxSide = 16383;

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
