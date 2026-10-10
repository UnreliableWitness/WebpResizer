using System.Diagnostics;
using BenchmarkDotNet.Running;
using WebpResizer.Benchmarks;

Settings.SingleThreadPerOp();
string mode = args.Length > 0 ? args[0] : "help";

switch (mode)
{
    case "prepare":
        Images.Prepare(args.Length > 1 ? args[1] : Images.Dir);
        Console.WriteLine("Benchmark images ready.");
        break;

    case "check":
    {
        // Smoke test: every library on every input, with output sizes. Writes outputs for visual inspection.
        string outDir = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "webpresizer-check");
        Directory.CreateDirectory(outDir);
        foreach (var name in Images.Names)
        foreach (int w in new[] { 1600, 400 })
        foreach (var p in Registry.All)
        {
            var bytes = Images.Load(name);
            var sw = Stopwatch.StartNew();
            var o = p.Process(bytes, w);
            sw.Stop();
            string file = $"{Path.GetFileNameWithoutExtension(name)}_{w}_{new string(p.Name.Where(char.IsLetterOrDigit).ToArray())}.webp";
            File.WriteAllBytes(Path.Combine(outDir, file), o);
            Console.WriteLine($"{name,-16} {w,5} {p.Name,-26} {o.Length,9:N0} B  {sw.Elapsed.TotalMilliseconds,8:F1} ms (cold)");
        }
        Console.WriteLine($"Outputs in {outDir}");
        break;
    }

    case "throughput":
    {
        // All cores, one image per worker, mixed workload of every input at 1600 and 400 px.
        // Run one library per process so peak memory is attributable.
        string lib = args.Length > 1 ? args[1] : "WebpResizer";
        int seconds = args.Length > 2 ? int.Parse(args[2]) : 30;
        var p = Registry.All.SingleOrDefault(x => x.Name == lib)
            ?? throw new ArgumentException($"Unknown library '{lib}'. Choose one of: {string.Join(", ", Registry.All.Select(x => x.Name))}");
        var inputs = Images.Names.Select(Images.Load).ToArray();
        var jobs = (from i in inputs from w in new[] { 1600, 400 } select (i, w)).ToArray();
        foreach (var j in jobs) p.Process(j.i, j.w); // warm-up

        int workers = Environment.ProcessorCount;
        long done = 0;
        var clock = Stopwatch.StartNew();
        var threads = Enumerable.Range(0, workers).Select(t => new Thread(() =>
        {
            int k = t;
            while (clock.Elapsed.TotalSeconds < seconds)
            {
                var j = jobs[k++ % jobs.Length];
                p.Process(j.i, j.w);
                Interlocked.Increment(ref done);
            }
        })).ToList();
        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        double secs = clock.Elapsed.TotalSeconds;
        long peakMb = Process.GetCurrentProcess().PeakWorkingSet64 / (1024 * 1024);
        Console.WriteLine($"{lib}: {done / secs:F2} images/s on {workers} threads ({done} in {secs:F1} s), peak memory {peakMb} MB");
        break;
    }

    case "bench":
        BenchmarkSwitcher.FromAssembly(typeof(Settings).Assembly).Run(args.Skip(1).ToArray());
        break;

    default:
        Console.WriteLine("""
            Usage:
              prepare [dir]                 build the 24 MP and alpha PNG inputs from photo-6mp.jpg
              check [out-dir]               smoke test every library, print sizes and cold timings
              bench --filter "*"            BenchmarkDotNet latency suites (ResizeToWebp, EncoderEffort)
              throughput [library] [secs]   multi-core images/s and peak memory for one library
            Images are read from IMG_DIR (default: ./images next to the binary).
            """);
        break;
}
