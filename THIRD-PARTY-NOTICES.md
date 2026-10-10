# Third-party notices

WebpResizer itself is MIT licensed. At runtime it loads these components, which keep their own licences.

| Component | Licence | Notes |
|---|---|---|
| [NetVips](https://github.com/kleisauke/net-vips) | MIT | .NET binding, referenced as a NuGet package. |
| [NetVips.Native](https://github.com/kleisauke/net-vips) | Bundles the libraries below | Prebuilt libvips for linux-x64/arm64, win-x64/arm64, osx. |
| [libvips](https://github.com/libvips/libvips) | LGPL-2.1-or-later | Dynamically linked, so it can be replaced; commercial use is allowed. |
| GLib and other GNOME libraries in libvips | LGPL-2.1-or-later | Dynamically linked. |
| [libwebp](https://chromium.googlesource.com/webm/libwebp) | BSD-3-Clause | WebP encoder and decoder. |
| [libjpeg-turbo](https://libjpeg-turbo.org/) | IJG, BSD-3-Clause, zlib | JPEG decoder. |
| libpng, zlib, and the remaining codecs in NetVips.Native | Various permissive licences | |

The complete list and licence texts for every bundled native library ship inside the NetVips.Native
runtime packages (`THIRD-PARTY-NOTICES.md` in each `NetVips.Native.<rid>` package), and in the
[libvips build repository](https://github.com/kleisauke/libvips-packaging). When you distribute an app
that uses WebpResizer, include those notices with it.

The benchmark project (not shipped) also references BenchmarkDotNet (MIT), SkiaSharp (MIT) and
SixLabors.ImageSharp 3.1.12 (Six Labors Split License) for comparison.
