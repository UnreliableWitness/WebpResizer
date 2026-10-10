using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;

namespace WebpResizer.Benchmarks;

/// Single-core latency of the full pipeline (bytes in -> WebP bytes out). Inputs are in memory, so no disk I/O.
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 10)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
[HideColumns(Column.Error, Column.StdDev, Column.RatioSD, Column.Gen0, Column.Gen1, Column.Gen2)]
public class ResizeToWebp
{
    [ParamsSource(nameof(Inputs))] public string Input = "";
    [Params(1600, 400)] public int Width;
    public static IEnumerable<string> Inputs => Images.Names;
    byte[] _bytes = [];

    [GlobalSetup] public void Setup()
    {
        Settings.SingleThreadPerOp();
        _bytes = Images.Load(Input);
    }

    static readonly WebpResizerProcessor Sdk = new();
    static readonly WebpResizerProcessor SdkEffort2 = new(effort: 2);
    static readonly RawNetVipsProcessor Raw = new();
    static readonly SkiaSharpProcessor Skia = new();
    static readonly ImageSharpProcessor IS = new();

    [Benchmark(Baseline = true, Description = "ImageSharp (paid ref)")] public byte[] ImageSharp() => IS.Process(_bytes, Width);
    [Benchmark(Description = "WebpResizer")] public byte[] WebpResizer() => Sdk.Process(_bytes, Width);
    [Benchmark(Description = "WebpResizer effort 2")] public byte[] WebpResizerEffort2() => SdkEffort2.Process(_bytes, Width);
    [Benchmark(Description = "NetVips (raw)")] public byte[] NetVipsRaw() => Raw.Process(_bytes, Width);
    [Benchmark(Description = "SkiaSharp (tuned)")] public byte[] SkiaSharp() => Skia.Process(_bytes, Width);
}

/// Encoder effort trade-off: time and output size for each effort level.
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 10)]
[HideColumns(Column.Error, Column.StdDev, Column.Gen0, Column.Gen1, Column.Gen2)]
public class EncoderEffort
{
    [Params("photo-24mp.jpg")] public string Input = "";
    [Params(1600)] public int Width;
    [Params(0, 2, 4, 6)] public int Effort;
    byte[] _bytes = [];
    WebpOptions _options = WebpOptions.Default;

    [GlobalSetup] public void Setup()
    {
        Settings.SingleThreadPerOp();
        _bytes = Images.Load(Input);
        _options = new WebpOptions { Width = Width, Quality = Settings.Quality, Effort = Effort };
    }

    [Benchmark] public byte[] Resize() => Webp.Resize(_bytes, _options).Data;
}
