namespace WebpResizer;

/// <summary>How the image is fitted to <see cref="WebpOptions.Width"/> × <see cref="WebpOptions.Height"/>.</summary>
public enum Fit
{
    /// <summary>Keep the aspect ratio and fit inside the box. A missing dimension is unbounded.</summary>
    Inside,
    /// <summary>
    /// Fill the box, cropping the overflow around the centre. Needs both dimensions. An input smaller than the box is
    /// cropped to the box but not enlarged unless <see cref="WebpOptions.AllowUpscale"/> is set, so the output can be smaller than the box.
    /// </summary>
    Cover,
    /// <summary>Like <see cref="Cover"/>, but crops around the most interesting region (libvips "attention").</summary>
    CoverSmart,
}

/// <summary>Resize and WebP encoder settings. The defaults match the benchmarked pipeline: lossy q80, effort 4, metadata stripped.</summary>
public sealed record WebpOptions
{
    /// <summary>Target width in pixels, or null for no width constraint.</summary>
    public int? Width { get; init; }

    /// <summary>Target height in pixels, or null for no height constraint.</summary>
    public int? Height { get; init; }

    /// <summary>How to fit the image to the target box. Default <see cref="Fit.Inside"/>.</summary>
    public Fit Fit { get; init; } = Fit.Inside;

    /// <summary>Allow output larger than the input. Default false: small images are only re-encoded.</summary>
    public bool AllowUpscale { get; init; }

    /// <summary>Lossy quality, 1–100. Default 80.</summary>
    public int Quality { get; init; } = 80;

    /// <summary>
    /// Encoder effort, 0 (fastest) to 6 (smallest files). Default 4, the libwebp default.
    /// Effort 2 encodes about 2.4× faster for files about 5% larger.
    /// </summary>
    public int Effort { get; init; } = 4;

    /// <summary>Encode losslessly. <see cref="Quality"/> is ignored. Default false.</summary>
    public bool Lossless { get; init; }

    /// <summary>Quality of the alpha channel, 0–100. Default 100.</summary>
    public int AlphaQuality { get; init; } = 100;

    /// <summary>Use sharper (slower) RGB→YUV conversion, which reduces colour bleeding on edges. Default false.</summary>
    public bool SmartSubsample { get; init; }

    /// <summary>Keep EXIF/XMP/ICC metadata. Default false (stripped, which also makes files smaller).</summary>
    public bool KeepMetadata { get; init; }

    /// <summary>
    /// Reject inputs with more pixels than this, as protection against decompression bombs.
    /// Default 268,402,689 (16383²). Null disables the check.
    /// </summary>
    public long? MaxInputPixels { get; init; } = 16383L * 16383L;

    /// <summary>Default options: no resize, lossy q80, effort 4.</summary>
    public static WebpOptions Default { get; } = new();

    internal void Validate()
    {
        if (Width is <= 0) throw new ArgumentOutOfRangeException(nameof(Width), Width, "Width must be positive.");
        if (Height is <= 0) throw new ArgumentOutOfRangeException(nameof(Height), Height, "Height must be positive.");
        if (Quality is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(Quality), Quality, "Quality must be 1–100.");
        if (Effort is < 0 or > 6) throw new ArgumentOutOfRangeException(nameof(Effort), Effort, "Effort must be 0–6.");
        if (AlphaQuality is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(AlphaQuality), AlphaQuality, "AlphaQuality must be 0–100.");
        if (MaxInputPixels is <= 0) throw new ArgumentOutOfRangeException(nameof(MaxInputPixels), MaxInputPixels, "MaxInputPixels must be positive.");
        if (Fit != Fit.Inside && (Width is null || Height is null))
            throw new ArgumentException($"Fit.{Fit} needs both Width and Height.", nameof(Fit));
    }
}
