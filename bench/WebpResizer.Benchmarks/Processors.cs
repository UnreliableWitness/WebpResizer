using SkiaSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using ISImage = SixLabors.ImageSharp.Image;

namespace WebpResizer.Benchmarks;

/// One pipeline: encoded bytes in -> resized to a width (aspect kept) -> lossy WebP q80 out.
public interface IProcessor
{
    string Name { get; }
    byte[] Process(byte[] input, int width);
}

public static class Settings
{
    public const int Quality = 80;
    public const int Effort = 4;

    /// One thread per operation for every library, so latency is single-core and throughput parallelises across images.
    public static void SingleThreadPerOp()
    {
        WebpRuntime.Configure(threadsPerOperation: 1, enableCache: false);
        SixLabors.ImageSharp.Configuration.Default.MaxDegreeOfParallelism = 1;
    }
}

/// This SDK.
public sealed class WebpResizerProcessor(int effort = Settings.Effort) : IProcessor
{
    public string Name => effort == Settings.Effort ? "WebpResizer" : $"WebpResizer (effort {effort})";
    public byte[] Process(byte[] input, int width) =>
        Webp.Resize(input, new WebpOptions { Width = width, Quality = Settings.Quality, Effort = effort }).Data;
}

/// The same pipeline calling NetVips directly, to measure the SDK's overhead.
public sealed class RawNetVipsProcessor : IProcessor
{
    public string Name => "NetVips (raw)";
    public byte[] Process(byte[] input, int width)
    {
        using var img = NetVips.Image.ThumbnailBuffer(input, width, height: 10_000_000, size: NetVips.Enums.Size.Down);
        return img.WebpsaveBuffer(q: Settings.Quality, effort: Settings.Effort, keep: NetVips.Enums.ForeignKeep.None);
    }
}

/// SkiaSharp with codec downscaling and a Mitchell resize. Skia's WebP encoder fixes method at 3.
public sealed class SkiaSharpProcessor : IProcessor
{
    public string Name => "SkiaSharp (tuned)";
    public byte[] Process(byte[] input, int width)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(input)) ?? throw new InvalidOperationException("decode");
        var full = codec.Info;
        int height = Math.Max(1, (int)Math.Round(full.Height * (double)width / full.Width));
        var dims = codec.GetScaledDimensions(Math.Min(1f, 2f * width / full.Width));
        if (dims.Width < width) dims = full.Size;
        var alpha = full.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Unpremul;
        var decInfo = new SKImageInfo(dims.Width, dims.Height, SKColorType.Rgba8888, alpha);
        using var decoded = new SKBitmap(decInfo);
        var r = codec.GetPixels(decInfo, decoded.GetPixels());
        if (r != SKCodecResult.Success && r != SKCodecResult.IncompleteInput) throw new InvalidOperationException(r.ToString());
        using var resized = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, alpha));
        if (!decoded.ScalePixels(resized, new SKSamplingOptions(SKCubicResampler.Mitchell))) throw new InvalidOperationException("scale");
        using var data = resized.Encode(SKEncodedImageFormat.Webp, Settings.Quality);
        return data.ToArray();
    }
}

/// Reference point: ImageSharp (Six Labors Split License, paid for commercial use above a revenue threshold).
public sealed class ImageSharpProcessor : IProcessor
{
    static readonly WebpEncoder Encoder = new() { Quality = Settings.Quality, Method = WebpEncodingMethod.Level4, FileFormat = WebpFileFormatType.Lossy };
    public string Name => "ImageSharp (paid ref)";
    public byte[] Process(byte[] input, int width)
    {
        using var img = ISImage.Load(input);
        img.Mutate(x => x.Resize(width, 0));
        using var ms = new MemoryStream();
        img.Save(ms, Encoder);
        return ms.ToArray();
    }
}

public static class Registry
{
    public static readonly IProcessor[] All =
    [
        new WebpResizerProcessor(),
        new WebpResizerProcessor(effort: 2),
        new RawNetVipsProcessor(),
        new SkiaSharpProcessor(),
        new ImageSharpProcessor(),
    ];
}
