using System;
using System.Diagnostics;

namespace Bench;

/// <summary>One measured repetition of a scenario: N iterations after a discarded warmup.</summary>
internal sealed class Measurement
{
    public string Scenario { get; init; } = "";

    public int Repetition { get; init; }

    public int Iterations { get; init; }

    /// <summary>
    /// Median per-file time. The headline number, because it is the only one that survives a
    /// loaded machine - see the note on <see cref="MeanMs"/>.
    /// </summary>
    public double MedianMs { get; init; }

    /// <summary>
    /// Mean per-file time over every sample. Comparable to the handoff's 0.0477 ms figure, but on
    /// a machine running other work it is dominated by a handful of multi-millisecond outliers
    /// (OS descheduling, gen2 GC) rather than by the code under test.
    /// </summary>
    public double MeanMs { get; init; }

    /// <summary>Mean of the samples after discarding the slowest 5%. Keeps ordinary GC cost, drops the pathological tail.</summary>
    public double TrimmedMeanMs { get; init; }

    public double MinMs { get; init; }

    public double MaxMs { get; init; }

    public double StdDevMs { get; init; }

    public double P95Ms { get; init; }

    /// <summary>Whole-loop wall time / iterations. Includes the per-iteration timer calls; a sanity check on <see cref="MeanMs"/>.</summary>
    public double WallMsPerFile { get; init; }

    /// <summary>Mean per-file managed allocation, from GC.GetAllocatedBytesForCurrentThread() deltas.</summary>
    public double KbPerFile { get; init; }

    public int OutputChars { get; init; }

    public string OutputHash { get; init; } = "";

    public int Gen0 { get; init; }

    public int Gen1 { get; init; }

    public int Gen2 { get; init; }

    private static readonly double MsPerTick = 1000.0 / Stopwatch.Frequency;

    /// <summary>
    /// Runs the scenario until it has done at least <paramref name="minIterations"/> iterations
    /// AND spent at least <paramref name="minMilliseconds"/> doing them, discarding everything.
    /// </summary>
    /// <remarks>
    /// A count-only warmup is not enough on .NET 8. Tiered compilation promotes on a call-count
    /// threshold but only after a 100 ms call-counting delay that restarts every time new methods
    /// are jitted, and dynamic PGO runs the instrumented tier until then - so a short warmup leaves
    /// the first measured repetition several times slower than the rest. Requiring wall time as
    /// well as a count is what makes repetition 1 agree with repetition 3.
    /// </remarks>
    public static long Warmup(Func<string> generate, int minIterations, int minMilliseconds)
    {
        long sink = 0;
        var iterations = 0;
        var start = Stopwatch.GetTimestamp();

        while (true)
        {
            sink += generate().Length;
            iterations++;

            if (iterations >= minIterations &&
                (Stopwatch.GetTimestamp() - start) * MsPerTick >= minMilliseconds)
            {
                break;
            }
        }

        return sink;
    }

    /// <summary>
    /// Runs <paramref name="warmup"/> discarded iterations, then <paramref name="iterations"/>
    /// measured ones, timing and allocation-counting each iteration individually.
    /// </summary>
    public static Measurement Run(string scenario, int repetition, Func<string> generate, int warmup, int iterations)
    {
        long sink = 0;

        for (var i = 0; i < warmup; i++)
        {
            sink += generate().Length;
        }

        var timeSamples = new double[iterations];
        var allocSamples = new long[iterations];

        // Start each repetition from a settled heap so the first iterations do not pay for the warmup's garbage.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);

        var outputChars = 0;
        var outputHash = "";

        var wallStart = Stopwatch.GetTimestamp();

        for (var i = 0; i < iterations; i++)
        {
            var allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();

            var output = generate();

            var end = Stopwatch.GetTimestamp();
            var allocAfter = GC.GetAllocatedBytesForCurrentThread();

            timeSamples[i] = (end - start) * MsPerTick;
            allocSamples[i] = allocAfter - allocBefore;

            sink += output.Length;

            if (i == 0)
            {
                outputChars = output.Length;
                outputHash = Hash(output);
            }
        }

        var wallEnd = Stopwatch.GetTimestamp();

        if (sink == long.MinValue)
        {
            // Never true; keeps `sink` (and therefore every generated string) observably live.
            Console.WriteLine(sink);
        }

        var measurement = Summarise(timeSamples, allocSamples, iterations);

        return new Measurement
        {
            Scenario = scenario,
            Repetition = repetition,
            Iterations = iterations,
            MeanMs = measurement.MeanMs,
            MedianMs = measurement.MedianMs,
            TrimmedMeanMs = measurement.TrimmedMeanMs,
            MinMs = measurement.MinMs,
            MaxMs = measurement.MaxMs,
            StdDevMs = measurement.StdDevMs,
            P95Ms = measurement.P95Ms,
            KbPerFile = measurement.KbPerFile,
            WallMsPerFile = (wallEnd - wallStart) * MsPerTick / iterations,
            OutputChars = outputChars,
            OutputHash = outputHash,
            Gen0 = GC.CollectionCount(0) - gen0,
            Gen1 = GC.CollectionCount(1) - gen1,
            Gen2 = GC.CollectionCount(2) - gen2,
        };
    }

    private static Measurement Summarise(double[] timeSamples, long[] allocSamples, int iterations)
    {
        double total = 0;
        double min = double.MaxValue;
        double max = double.MinValue;

        foreach (var sample in timeSamples)
        {
            total += sample;
            if (sample < min) min = sample;
            if (sample > max) max = sample;
        }

        var mean = total / iterations;

        double sumOfSquares = 0;
        foreach (var sample in timeSamples)
        {
            var delta = sample - mean;
            sumOfSquares += delta * delta;
        }

        var sorted = (double[])timeSamples.Clone();
        Array.Sort(sorted);

        var keep = (int)(iterations * 0.95);
        if (keep < 1) keep = iterations;

        double trimmedTotal = 0;
        for (var i = 0; i < keep; i++)
        {
            trimmedTotal += sorted[i];
        }

        long allocTotal = 0;
        foreach (var sample in allocSamples)
        {
            allocTotal += sample;
        }

        return new Measurement
        {
            MeanMs = mean,
            MedianMs = sorted[sorted.Length / 2],
            TrimmedMeanMs = trimmedTotal / keep,
            MinMs = min,
            MaxMs = max,
            StdDevMs = iterations > 1 ? Math.Sqrt(sumOfSquares / (iterations - 1)) : 0,
            P95Ms = sorted[(int)(sorted.Length * 0.95)],
            KbPerFile = allocTotal / (double)iterations / 1024.0,
        };
    }

    /// <summary>A short stable digest of the generated text, so a V1/V2 output change is visible in the results.</summary>
    public static string Hash(string text)
    {
        // FNV-1a 64. Not cryptographic; it only has to change when the text does.
        unchecked
        {
            var hash = 14695981039346656037UL;

            foreach (var c in text)
            {
                hash ^= c;
                hash *= 1099511628211UL;
            }

            return hash.ToString("x16");
        }
    }
}
