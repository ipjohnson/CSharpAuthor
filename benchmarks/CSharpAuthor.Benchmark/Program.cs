using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime;

namespace Bench;

/// <summary>
/// Gate 9 harness for V2-HANDOFF.md §10.
///
/// Emits one TSV `RESULT` line per repetition on stdout so scripts/run-benchmark.sh can
/// aggregate across processes and across checkouts, plus a human summary on stderr.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var iterations = 2000;
        var warmup = 1000;
        var warmupMs = 1500;
        var repetitions = 3;
        var label = "csharpauthor";
        var scenarios = new List<string> { "tree", "stringbuilder" };
        var dump = false;
        var verify = false;
        var quiet = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--iterations": iterations = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--warmup": warmup = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--warmup-ms": warmupMs = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--reps": repetitions = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--label": label = args[++i]; break;
                case "--scenarios": scenarios = new List<string>(args[++i].Split(',')); break;
                case "--dump": dump = true; break;
                case "--verify": verify = true; break;
                case "--quiet": quiet = true; break;
                case "--help":
                    Console.Error.WriteLine(
                        "usage: CSharpAuthor.Benchmark [--iterations N] [--warmup N] [--warmup-ms N] " +
                        "[--reps N] [--label NAME] [--scenarios tree,stringbuilder,roslyn] " +
                        "[--dump] [--verify] [--quiet]");
                    return 0;
                default:
                    Console.Error.WriteLine("unknown argument: " + args[i]);
                    return 2;
            }
        }

        if (dump)
        {
            Console.Out.Write(TreePayload.Generate());
            return 0;
        }

        if (verify)
        {
            return Verify(label);
        }

        var generators = new List<KeyValuePair<string, Func<string>>>();

        foreach (var scenario in scenarios)
        {
            switch (scenario.Trim())
            {
                case "tree":
                    generators.Add(new KeyValuePair<string, Func<string>>("tree", TreePayload.Generate));
                    break;
                case "stringbuilder":
                    generators.Add(new KeyValuePair<string, Func<string>>("stringbuilder", StringBuilderPayload.Generate));
                    break;
                case "roslyn":
#if BENCH_ROSLYN
                    generators.Add(new KeyValuePair<string, Func<string>>("roslyn", RoslynPayload.Generate));
#else
                    Console.Error.WriteLine(
                        "note: the roslyn reference point was not compiled in; " +
                        "rebuild with -p:IncludeRoslynReference=true. Skipping it.");
#endif
                    break;
                case "":
                    break;
                default:
                    Console.Error.WriteLine("unknown scenario: " + scenario);
                    return 2;
            }
        }

        if (!quiet)
        {
            Console.Error.WriteLine(
                $"# label={label} iterations={iterations} warmup={warmup}/{warmupMs}ms reps={repetitions} " +
                $"serverGC={GCSettings.IsServerGC} latency={GCSettings.LatencyMode} " +
                $"runtime={Environment.Version} cpus={Environment.ProcessorCount}");
        }

        // Warm every scenario to tier-1 before anything is recorded, so no scenario is measured on
        // a colder process than another and repetition 1 is comparable with repetition 3.
        foreach (var generator in generators)
        {
            Measurement.Warmup(generator.Value, warmup, warmupMs);
        }

        var results = new List<Measurement>();

        for (var repetition = 1; repetition <= repetitions; repetition++)
        {
            foreach (var generator in generators)
            {
                // 200 further iterations inside Run keep the scenario hot across the interleave.
                var measurement = Measurement.Run(generator.Key, repetition, generator.Value, 200, iterations);

                results.Add(measurement);

                Console.Out.WriteLine(string.Join(
                    "\t",
                    "RESULT",
                    label,
                    measurement.Scenario,
                    measurement.Repetition.ToString(CultureInfo.InvariantCulture),
                    measurement.Iterations.ToString(CultureInfo.InvariantCulture),
                    F(measurement.MedianMs),
                    F(measurement.TrimmedMeanMs),
                    F(measurement.MeanMs),
                    F(measurement.MinMs),
                    F(measurement.MaxMs),
                    F(measurement.StdDevMs),
                    F(measurement.P95Ms),
                    F(measurement.WallMsPerFile),
                    measurement.KbPerFile.ToString("F3", CultureInfo.InvariantCulture),
                    measurement.OutputChars.ToString(CultureInfo.InvariantCulture),
                    measurement.OutputHash,
                    $"gc={measurement.Gen0}/{measurement.Gen1}/{measurement.Gen2}"));

                Console.Out.Flush();
            }
        }

        if (!quiet)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                $"{"scenario",-14} {"rep",3} {"median ms",10} {"trimmed",10} {"mean ms",10} " +
                $"{"KB/file",9} {"min",9} {"max",9}");

            foreach (var measurement in results)
            {
                Console.Error.WriteLine(
                    $"{measurement.Scenario,-14} {measurement.Repetition,3} {measurement.MedianMs,10:F4} " +
                    $"{measurement.TrimmedMeanMs,10:F4} {measurement.MeanMs,10:F4} {measurement.KbPerFile,9:F1} " +
                    $"{measurement.MinMs,9:F4} {measurement.MaxMs,9:F4}");
            }
        }

        return 0;
    }

    private static string F(double value) => value.ToString("F6", CultureInfo.InvariantCulture);

    /// <summary>
    /// Reports what the payload actually produced against the library under measurement, so the
    /// orchestrator can see whether V2 emits the same text as V1 rather than a different file at
    /// a different price.
    /// </summary>
    private static int Verify(string label)
    {
        var tree = TreePayload.Generate();
        var stringBuilder = StringBuilderPayload.Generate();

        Console.Out.WriteLine($"VERIFY\t{label}\ttree\t{tree.Length}\t{Measurement.Hash(tree)}");
        Console.Out.WriteLine(
            $"VERIFY\t{label}\tstringbuilder\t{stringBuilder.Length}\t{Measurement.Hash(stringBuilder)}");
        Console.Out.WriteLine(
            $"VERIFY\t{label}\tidentical\t{(string.Equals(tree, stringBuilder, StringComparison.Ordinal) ? "yes" : "no")}");

        if (!string.Equals(tree, stringBuilder, StringComparison.Ordinal))
        {
            var limit = Math.Min(tree.Length, stringBuilder.Length);
            var index = 0;

            while (index < limit && tree[index] == stringBuilder[index])
            {
                index++;
            }

            Console.Error.WriteLine($"# first difference at char {index}");
            Console.Error.WriteLine("# tree: " + Excerpt(tree, index));
            Console.Error.WriteLine("# sb  : " + Excerpt(stringBuilder, index));
        }

        return 0;
    }

    private static string Excerpt(string text, int index)
    {
        var start = Math.Max(0, index - 40);
        var length = Math.Min(80, text.Length - start);

        return text.Substring(start, length).Replace("\n", "\\n");
    }
}
