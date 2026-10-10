# WebpResizer

Fast, free image resizing and WebP encoding for .NET 8 and .NET 10, on Linux and Windows.

It is a small API over [libvips](https://www.libvips.org/) (through [NetVips](https://github.com/kleisauke/net-vips)), which
in our benchmarks was about **2.4× the throughput of ImageSharp** with the best output quality of the libraries tested,
and has no commercial licence fee. The native libraries come from NuGet, so there is nothing to install on the server.

```csharp
using WebpResizer;

// Bytes in, WebP bytes out. Aspect ratio is kept; images are never upscaled unless you ask.
WebpResult result = Webp.Resize(jpegBytes, new WebpOptions { Width = 1600 });
File.WriteAllBytes("photo.webp", result.Data);   // result.Width, result.Height are the output size

// Files (streams from disk where the format allows)
Webp.ResizeFile("in.jpg", "out.webp", new WebpOptions { Width = 400, Height = 400, Fit = Fit.Cover });

// Streams, e.g. an upload in ASP.NET Core
await Webp.ResizeAsync(upload.OpenReadStream(), Response.Body, new WebpOptions { Width = 800 }, ct);

// Convert only, no resize
Webp.Resize(pngBytes);
```

## Options

| Option | Default | Meaning |
|---|---|---|
| `Width`, `Height` | none | Target box. Leave one out to constrain only the other; leave both out to convert without resizing. |
| `Fit` | `Inside` | `Inside` keeps the whole image inside the box. `Cover` fills the box and crops around the centre; `CoverSmart` crops around the most interesting region. Inputs smaller than the box are not enlarged unless `AllowUpscale` is set. |
| `AllowUpscale` | `false` | Allow output larger than the input. |
| `Quality` | `80` | Lossy quality, 1–100. |
| `Effort` | `4` | Encoder effort, 0 (fastest) to 6 (smallest). See below. |
| `Lossless` | `false` | Lossless WebP; `Quality` is ignored. |
| `AlphaQuality` | `100` | Quality of the transparency channel, 0–100. |
| `SmartSubsample` | `false` | Sharper colour edges, slower encode. |
| `KeepMetadata` | `false` | Keep EXIF/XMP/ICC. Stripped by default, which makes files smaller. |
| `MaxInputPixels` | 16383² | Reject larger inputs (decompression bomb guard). `null` turns it off. Output sides are also limited to 16383 px by the WebP format. |

EXIF orientation is always applied. Inputs can be JPEG, PNG, WebP, GIF, TIFF, HEIF/AVIF and the other formats libvips reads.
Failures to decode or encode throw `ImageProcessingException`; bad options throw `ArgumentException`.

### Choosing the encoder effort

WebP encoding is most of the work, so `Effort` is the main speed knob. Measured on a 24 MP photo resized to 1600 px,
whole pipeline (decode, resize and encode) on a 4 vCPU GitHub runner:

| Effort | Time | Output size | SSIM |
|---:|---:|---:|---:|
| 0 | 119 ms | 212,544 B | 0.957 |
| 2 | 142 ms | 166,410 B | 0.958 |
| **4** (default) | 246 ms | 158,226 B | 0.960 |
| 6 | 404 ms | 148,954 B | 0.958 |

Effort 2 is a good choice when throughput matters more than the last 5% of file size: about 1.7× faster end to end.

## Threading

All methods are thread-safe. By default each image is processed on one thread with the libvips cache off, which gives
the best throughput when a server handles several images at once; parallelise across images in your own code
(requests, `Parallel.ForEachAsync`, a queue). If you mostly process one large image at a time, let libvips use more cores
before the first call:

```csharp
WebpRuntime.Configure(threadsPerOperation: Environment.ProcessorCount);
```

## Benchmarks

The benchmark project compares WebpResizer with raw NetVips, SkiaSharp and ImageSharp on the same pipeline
(resize to 1600 and 400 px wide, lossy q80, effort 4, metadata stripped).

```sh
./scripts/get-images.sh images          # or scripts/get-images.ps1 on Windows
export IMG_DIR=$PWD/images
cd bench/WebpResizer.Benchmarks
dotnet run -c Release -- check                          # smoke test and output sizes
dotnet run -c Release -- bench --filter "*"             # BenchmarkDotNet latency suites
dotnet run -c Release -- throughput "WebpResizer" 30    # images/s on all cores, plus peak memory
```

To benchmark your own photos, put them in a folder named like the defaults (`photo-6mp.jpg`, `photo-24mp.jpg`,
`alpha-3mp.png`) and point `IMG_DIR` at it. The **Benchmarks** workflow in GitHub Actions runs the suite on Linux and
Windows on demand; shared runners are noisy, so compare ratios rather than absolute times.

Results from the Benchmarks workflow on `ubuntu-latest` (AMD EPYC 9V45, 4 vCPU, Ubuntu 24.04, one thread per image):

| Library | Images/s (4 cores, mixed workload) | vs ImageSharp | Peak memory |
|---|---:|---:|---:|
| **libvips (WebpResizer)** | **19.3** | **2.4×** | 572 MB |
| libvips (WebpResizer, effort 2) | 30.6 | 3.7× | 498 MB |
| NetVips (raw) | 19.1 | 2.3× | 581 MB |
| SkiaSharp (tuned) | 17.6 | 2.1× | 393 MB |
| ImageSharp 3.1 | 8.2 | 1.0× | 698 MB |

The same run on `windows-latest` gave 12.0 images/s for WebpResizer against 3.3 for ImageSharp (3.7×). An earlier, broader
study on a 4 vCPU Xeon also measured Magick.NET Q8 at 1.2× ImageSharp and SkiaSharp without codec tuning at 1.6×.

WebpResizer's own overhead over calling NetVips directly is within measurement noise.

## Licence

MIT. libvips and some of its dependencies are LGPL-2.1 and are dynamically linked, which allows commercial use; ship the
notices listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) with your app.
