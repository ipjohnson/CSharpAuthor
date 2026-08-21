using System;
using System.Collections.Generic;
using System.IO;

namespace CSharpAuthor.Docs.Samples;

/// <summary>
/// Runs every sample the documentation site shows and checks its output against the recording
/// in <c>expected/</c>, which is the text the site prints as the result.
/// </summary>
/// <remarks>
/// <para>
/// The site never contains a hand-typed sample. Each C# block is a <c>#region</c> of a file in
/// this project, pulled in by DocFX's code-include syntax, and each "and this comes out" block is
/// the matching <c>expected/*.txt</c>. So a sample that stops compiling breaks this project, and a
/// sample whose output changes fails this check - neither can reach the site quietly.
/// </para>
/// <para>
///   <c>dotnet run --project docfx/samples/CSharpAuthor.Docs.Samples</c> verifies.
///   <c>… -- --update</c> re-records.
/// </para>
/// </remarks>
public static class Program
{
    private static readonly List<(string Name, Func<string> Run)> Samples =
    [
        ("getting-started-smallest", GettingStarted.Smallest),
        ("getting-started-greeter", GettingStarted.Greeter),

        ("type-model-constructing", TypeModelSamples.Constructing),
        ("type-model-one-tree-two-renderings", TypeModelSamples.OneTreeTwoRenderings),
        ("type-model-shapes", TypeModelSamples.Shapes),

        ("output-modes-three-modes", OutputModeSamples.ThreeModes),
        ("output-modes-collision-aliasing", OutputModeSamples.CollisionAliasing),
        ("output-modes-members-off-a-type", OutputModeSamples.MembersOffAType),
        ("output-modes-extension-usings", OutputModeSamples.ExtensionMethodsNeedAUsing),
        ("output-modes-containing-namespace", OutputModeSamples.ContainingNamespace),

        ("emit-profiles-same-tree-two-targets", EmitProfileSamples.SameTreeTwoTargets),
        ("emit-profiles-polyfilled-init", EmitProfileSamples.PolyfilledInit),
        ("emit-profiles-diagnostic-channel", EmitProfileSamples.DiagnosticChannel),
        ("emit-profiles-capability-violation", EmitProfileSamples.CapabilityViolation),
        ("emit-profiles-from-editorconfig", EmitProfileSamples.FromEditorConfig),
        ("emit-profiles-brace-style", EmitProfileSamples.BraceStyleThroughOptions),
    ];

    public static int Main(string[] args)
    {
        var update = Array.IndexOf(args, "--update") >= 0;
        var directory = ExpectedDirectory();

        Directory.CreateDirectory(directory);

        var failures = 0;

        foreach (var (name, run) in Samples)
        {
            var path = Path.Combine(directory, name + ".txt");

            string actual;

            try
            {
                actual = Normalise(run());
            }
            catch (Exception exception)
            {
                Console.WriteLine($"THREW  {name}: {exception.GetType().Name}: {exception.Message}");
                failures++;
                continue;
            }

            if (update)
            {
                File.WriteAllText(path, actual);
                Console.WriteLine($"WROTE  {name}  ({actual.Length} chars)");
                continue;
            }

            if (!File.Exists(path))
            {
                Console.WriteLine($"MISSING  {name}: no recording at {path}");
                failures++;
                continue;
            }

            var expected = Normalise(File.ReadAllText(path));

            if (expected == actual)
            {
                Console.WriteLine($"OK     {name}");
                continue;
            }

            failures++;
            Console.WriteLine($"DIFFER {name}");
            Console.WriteLine("--- recorded ---");
            Console.WriteLine(expected);
            Console.WriteLine("--- produced ---");
            Console.WriteLine(actual);
        }

        Console.WriteLine();
        Console.WriteLine(update
            ? $"{Samples.Count} samples re-recorded."
            : $"{Samples.Count - failures}/{Samples.Count} samples match their recorded output.");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>Walks up from the assembly to the project directory, so the run needs no cwd.</summary>
    private static string ExpectedDirectory()
    {
        var directory = AppContext.BaseDirectory;

        while (directory is { Length: > 0 })
        {
            if (File.Exists(Path.Combine(directory, "CSharpAuthor.Docs.Samples.csproj")))
            {
                return Path.Combine(directory, "expected");
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException("could not find CSharpAuthor.Docs.Samples.csproj above " + AppContext.BaseDirectory);
    }

    /// <summary>
    /// Line endings only. The library emits <c>\n</c> by default; git on Windows may hand back
    /// <c>\r\n</c>, and that difference is not a sample changing.
    /// </summary>
    private static string Normalise(string text) => text.Replace("\r\n", "\n");
}
