namespace WebpResizer;

/// <summary>
/// Process-wide libvips settings. By default each operation runs on one thread with the operation cache off,
/// which gave the best throughput when a server processes several images in parallel. Call
/// <see cref="Configure"/> before the first resize to change this, e.g. to let a single large image use more cores.
/// </summary>
public static class WebpRuntime
{
    static readonly object Gate = new();
    static bool _configured;

    /// <summary>Applies libvips settings. Safe to call more than once; the last call wins.</summary>
    /// <param name="threadsPerOperation">libvips worker threads per image. 1 is best for parallel workloads.</param>
    /// <param name="enableCache">Enable the libvips operation cache. Off by default; it rarely helps for one-shot resizes.</param>
    public static void Configure(int threadsPerOperation = 1, bool enableCache = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(threadsPerOperation, 1);
        lock (Gate)
        {
            if (!NetVips.ModuleInitializer.VipsInitialized)
                throw new ImageProcessingException("libvips failed to load.", NetVips.ModuleInitializer.Exception);
            NetVips.NetVips.Concurrency = threadsPerOperation;
            if (enableCache)
            {
                NetVips.Cache.Max = 100;
                NetVips.Cache.MaxMem = 100 * 1024 * 1024;
                NetVips.Cache.MaxFiles = 100;
            }
            else
            {
                NetVips.Cache.Max = 0;
                NetVips.Cache.MaxMem = 0;
                NetVips.Cache.MaxFiles = 0;
            }
            _configured = true;
        }
    }

    /// <summary>The libvips version in use, e.g. "8.18.7".</summary>
    public static string LibvipsVersion
    {
        get
        {
            EnsureConfigured();
            return $"{NetVips.NetVips.Version(0)}.{NetVips.NetVips.Version(1)}.{NetVips.NetVips.Version(2)}";
        }
    }

    internal static void EnsureConfigured()
    {
        if (Volatile.Read(ref _configured)) return;
        lock (Gate)
        {
            if (!_configured) Configure();
        }
    }
}
