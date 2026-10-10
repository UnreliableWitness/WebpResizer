namespace WebpResizer.Benchmarks;

public static class Images
{
    public static readonly string[] Names = ["photo-6mp.jpg", "photo-24mp.jpg", "alpha-3mp.png"];

    public static string Dir => Environment.GetEnvironmentVariable("IMG_DIR") ?? Path.Combine(AppContext.BaseDirectory, "images");

    public static byte[] Load(string name)
    {
        var path = Path.Combine(Dir, name);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Benchmark image missing. Run the 'prepare' command first, or set IMG_DIR. Looked for {path}");
        return File.ReadAllBytes(path);
    }

    /// Builds the derived inputs from photo-6mp.jpg: a 2× lanczos upscale (24 MP JPEG) and a 3 MP RGBA PNG.
    public static void Prepare(string dir)
    {
        var source = Path.Combine(dir, "photo-6mp.jpg");
        if (!File.Exists(source))
            throw new FileNotFoundException($"Put a photo at {source} first (scripts/get-images downloads one).");

        using var photo = NetVips.Image.NewFromFile(source);
        using (var big = photo.Resize(2.0, kernel: NetVips.Enums.Kernel.Lanczos3))
            big.Jpegsave(Path.Combine(dir, "photo-24mp.jpg"), q: 90);

        using var small = photo.ThumbnailImage(2048, height: 1536, crop: NetVips.Enums.Interesting.Centre);
        using var xyz = NetVips.Image.Xyz(small.Width, small.Height);
        using var x = xyz[0];
        using var y = xyz[1];
        // Radial alpha: opaque centre fading to transparent corners.
        using var dx = x - small.Width / 2.0;
        using var dy = y - small.Height / 2.0;
        using var dist = (dx * dx + dy * dy).Pow(0.5);
        using var alpha = (255 - dist * (255.0 / Math.Sqrt(small.Width * small.Width / 4.0 + small.Height * small.Height / 4.0)))
            .Cast(NetVips.Enums.BandFormat.Uchar);
        using var rgba = small.Bandjoin(alpha);
        rgba.Pngsave(Path.Combine(dir, "alpha-3mp.png"));
    }
}
