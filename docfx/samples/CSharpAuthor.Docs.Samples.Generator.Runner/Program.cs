using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CSharpAuthor.Docs.Samples.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CSharpAuthor.Docs.Samples.GeneratorRunner;

/// <summary>
/// Runs the sample generator over a real compilation and checks its output against the recordings
/// the documentation site prints.
/// </summary>
public static class Program
{
    #region host-source
    private const string HostSource = """
        using System;
        using System.Collections.Generic;
        using Acme.Generated;

        namespace Acme.Inventory;

        [Describe]
        public partial class Widget
        {
            public string Sku { get; set; } = "";
            public int? Quantity { get; set; }
            public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
        }
        """;
    #endregion

    public static int Main(string[] args)
    {
        var update = Array.IndexOf(args, "--update") >= 0;
        var directory = ExpectedDirectory();

        Directory.CreateDirectory(directory);

        var produced = Run();
        var failures = 0;

        foreach (var (name, text) in produced)
        {
            var path = Path.Combine(directory, name + ".txt");
            var actual = text.Replace("\r\n", "\n");

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

            var expected = File.ReadAllText(path).Replace("\r\n", "\n");

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
            ? $"{produced.Count} generator outputs re-recorded."
            : $"{produced.Count - failures}/{produced.Count} generator outputs match their recorded output.");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>Drives the generator, and fails loudly rather than recording broken output.</summary>
    private static List<(string Name, string Text)> Run()
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp12);

        var syntaxTree = CSharpSyntaxTree.ParseText(HostSource, parseOptions, path: "Widget.cs");

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(static assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Cast<MetadataReference>()
            .ToList();

        references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(
            Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "netstandard.dll")));

        var compilation = CSharpCompilation.Create(
            "Acme.Inventory",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // parseOptions has to match the compilation's, or the driver parses what it generated at a
        // different language version than the rest of the compilation and Roslyn refuses.
        var driver = CSharpGeneratorDriver
            .Create(
                new[] { new DescribeGenerator().AsSourceGenerator() },
                parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out var updated, out var diagnostics);

        var generatorDiagnostics = diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error).ToList();

        if (generatorDiagnostics.Count > 0)
        {
            throw new InvalidOperationException(
                "the generator reported errors: " + string.Join("; ", generatorDiagnostics));
        }

        // The point of the exercise: what the generator produced has to compile.
        var compileErrors = updated.GetDiagnostics()
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (compileErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "generated code does not compile: " + string.Join("; ", compileErrors));
        }

        _ = driver;

        return updated.SyntaxTrees
            .Where(static tree => tree.FilePath.EndsWith(".g.cs", StringComparison.Ordinal))
            .Select(static tree => (
                Name: "source-generators-" + Path.GetFileNameWithoutExtension(tree.FilePath).Replace(".g", ""),
                Text: tree.ToString()))
            .OrderBy(static entry => entry.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static string ExpectedDirectory()
    {
        var directory = AppContext.BaseDirectory;

        while (directory is { Length: > 0 })
        {
            var candidate = Path.Combine(directory, "CSharpAuthor.Docs.Samples.Generator");

            if (File.Exists(Path.Combine(candidate, "CSharpAuthor.Docs.Samples.Generator.csproj")))
            {
                return Path.Combine(candidate, "expected");
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException(
            "could not find CSharpAuthor.Docs.Samples.Generator above " + AppContext.BaseDirectory);
    }
}
