// V2-HANDOFF.md 9(b): the round-trip fidelity measurement.
//
//   source file -> Roslyn parse -> import to CSharpAuthor tree -> emit -> Roslyn parse
//                -> compare the two trees for structural equivalence
//
// Reports files round-tripping / files attempted, with a histogram of the node kinds that
// failed, split into three buckets that are never conflated.
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CSharpAuthor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
// CSharpAuthor grew its own LanguageVersion (4's EmitProfile), which collides with
// Roslyn's for any file that has both usings - CS0104. Alias rather than drop a using: a
// source generator normally needs both namespaces, so this is the shape consumers will hit.
using RoslynLanguageVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion;

namespace RoundTrip;

internal enum LayerKind { Proto, Rt }

internal sealed class FileResult
{
    public string Path = "";
    public bool Passed;
    public bool SourceRejected;
    public List<Failure> Failures = new();
    public string? Emitted;
    /// <summary>Passed the cross-check verdict but not the primary one.</summary>
    public bool CrossCheckOnly;
}

internal static class Program
{
    private static int Main(string[] args)
    {
        string? repo = null, corpus = "own", outPath = null, only = null;
        var layers = new List<LayerKind>();
        var typeMode = TypeImportMode.Model;
        var dumpFirst = 0;
        var consumers = (string?)null;
        string? emitDir = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--repo": repo = args[++i]; break;
                case "--consumers": consumers = args[++i]; break;
                case "--corpus": corpus = args[++i]; break;
                case "--layer":
                    foreach (var l in args[++i].Split(','))
                        layers.Add(l.Trim().Equals("rt", StringComparison.OrdinalIgnoreCase) ? LayerKind.Rt : LayerKind.Proto);
                    break;
                case "--types":
                    typeMode = args[++i].Equals("verbatim", StringComparison.OrdinalIgnoreCase)
                        ? TypeImportMode.Verbatim : TypeImportMode.Model;
                    break;
                case "--out": outPath = args[++i]; break;
                case "--only": only = args[++i]; break;
                case "--dump-first": dumpFirst = int.Parse(args[++i]); break;
                case "--emit-dir": emitDir = args[++i]; break;
                default:
                    Console.Error.WriteLine($"unknown argument: {args[i]}");
                    return 2;
            }
        }

        if (repo == null)
        {
            Console.Error.WriteLine("usage: --repo <checkout> [--consumers <dir>] [--corpus own|dm|hardened|all] " +
                                    "[--layer proto|rt|proto,rt] [--types model|verbatim] [--out FILE]");
            return 2;
        }

        if (layers.Count == 0) layers.Add(LayerKind.Proto);

        var sets = CorpusSets(repo, consumers, corpus);
        if (sets.Count == 0)
        {
            Console.Error.WriteLine("no corpus found");
            return 2;
        }

        var report = new StringBuilder();
        void Emit(string line) { Console.WriteLine(line); report.AppendLine(line); }

        var roslyn = typeof(CSharpSyntaxTree).Assembly.GetName().Version?.ToString() ?? "?";
        var parseOptions = new CSharpParseOptions(
            RoslynLanguageVersion.CSharp13, DocumentationMode.None, SourceCodeKind.Regular);

        Emit("================================================================================");
        Emit("CSharpAuthor 2.0 - round-trip fidelity (V2-HANDOFF.md 9(b))");
        Emit("================================================================================");
        Emit($"Roslyn                 : Microsoft.CodeAnalysis.CSharp {roslyn} (package 4.14.0)");
        Emit($"Language version       : {parseOptions.LanguageVersion} - the ceiling this parser imposes.");
        Emit("                         Nothing above C# 13 is validated by this run.");
        Emit($"Preprocessor symbols   : (none)");
        Emit($"Type import            : {typeMode}");
        Emit($"Emit mode              : TypeOutputMode.FullName, no using-directive generation");
        Emit("Equivalence            : SyntaxNode.IsEquivalentTo(topLevel: false) - same kinds,");
        Emit("                         same shape, same token text; trivia (whitespace, comments,");
        Emit("                         XML docs, #region/#pragma/#nullable/#if) ignored.");
        Emit("");

        var overall = 0;
        foreach (var layer in layers)
        {
            foreach (var set in sets)
            {
                var results = Run(layer, set.Value, typeMode, parseOptions, only, dumpFirst);
                if (emitDir != null) DumpEmitted(emitDir, layer, set.Key, results);
                PrintSet(Emit, layer, set.Key, results, dumpFirst);
                if (results.Any(r => !r.Passed && !r.SourceRejected)) overall = 1;
            }
        }

        if (outPath != null) File.WriteAllText(outPath, report.ToString());
        return overall == 0 ? 0 : 1;
    }

    /// <summary>Write what the emitter produced, so a failure can be read rather than guessed at.</summary>
    private static void DumpEmitted(string dir, LayerKind layer, string set, List<FileResult> results)
    {
        var target = Path.Combine(dir, layer.ToString().ToLowerInvariant(), Sanitise(set));
        Directory.CreateDirectory(target);
        foreach (var r in results)
        {
            if (r.Emitted == null) continue;
            File.WriteAllText(Path.Combine(target, Sanitise(Path.GetFileNameWithoutExtension(r.Path)) + ".cs"), r.Emitted);
        }
    }

    private static string Sanitise(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Replace(' ', '_').Replace('(', '_').Replace(')', '_');
    }

    // ---------------------------------------------------------------- corpus

    private static Dictionary<string, List<string>> CorpusSets(string repo, string? consumers, string which)
    {
        var sets = new Dictionary<string, List<string>>();
        var wanted = which.Split(',').Select(s => s.Trim().ToLowerInvariant()).ToHashSet();
        var all = wanted.Contains("all");

        if (all || wanted.Contains("own"))
        {
            var dir = Path.Combine(repo, "CSharpAuthor");
            if (Directory.Exists(dir)) sets["own (CSharpAuthor)"] = Sources(dir);
        }

        consumers ??= FindConsumers(repo);
        if (consumers != null)
        {
            if (all || wanted.Contains("dm"))
            {
                var dir = Path.Combine(consumers, "DependencyModules");
                if (Directory.Exists(dir)) sets["DependencyModules"] = Sources(dir);
            }
            if (all || wanted.Contains("hardened"))
            {
                var dir = Path.Combine(consumers, "Hardened.Framework");
                if (Directory.Exists(dir)) sets["Hardened.Framework"] = Sources(dir);
            }
        }
        return sets;
    }

    private static string? FindConsumers(string repo)
    {
        var d = new DirectoryInfo(Path.GetFullPath(repo));
        while (d != null)
        {
            var c = Path.Combine(d.FullName, "consumers");
            if (Directory.Exists(c)) return c;
            d = d.Parent;
        }
        return null;
    }

    private static List<string> Sources(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                        && !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

    // ---------------------------------------------------------------- the round trip

    private static List<FileResult> Run(LayerKind layer, List<string> files, TypeImportMode typeMode,
                                        CSharpParseOptions options, string? only, int dumpFirst)
    {
        var results = new List<FileResult>(files.Count);
        foreach (var path in files)
        {
            if (only != null && !path.Contains(only)) continue;
            results.Add(RunOne(layer, path, typeMode, options));
        }
        return results;
    }

    private static FileResult RunOne(LayerKind layer, string path, TypeImportMode typeMode,
                                     CSharpParseOptions options)
    {
        var result = new FileResult { Path = path };
        var report = new ImportReport();

        string source;
        try { source = File.ReadAllText(path); }
        catch (Exception e)
        {
            report.Add(Bucket.Import, "file", "unreadable: " + e.Message);
            result.Failures = report.Failures;
            return result;
        }

        var original = CSharpSyntaxTree.ParseText(SourceText.From(source), options, path);
        var originalRoot = original.GetRoot();

        // A file the reference parser itself rejects is not evidence about the emitter.
        // Excluded from the denominator and counted separately, never silently passed.
        if (original.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            result.SourceRejected = true;
            var first = original.GetDiagnostics().First(d => d.Severity == DiagnosticSeverity.Error);
            report.Add(Bucket.Import, first.Id, "source does not parse at C# 13: " + Trim(first.GetMessage()));
            result.Failures = report.Failures;
            return result;
        }

        // 1. import ------------------------------------------------------------------
        string emitted;
        try
        {
            var context = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.FullName });
            if (layer == LayerKind.Proto)
            {
                var importer = new ProtoImporter(report, typeMode);
                var tree = importer.Import(originalRoot);
                if (report.ImportFailed || tree == null)
                {
                    if (tree == null && !report.ImportFailed)
                        report.Add(Bucket.Import, originalRoot.Kind().ToString(), "importer produced no tree");
                    result.Failures = report.Failures;
                    return result;
                }
                tree.WriteOutput(context);
            }
            else
            {
                var importer = new RtImporter(report, typeMode);
                var tree = importer.Import(originalRoot);
                if (report.ImportFailed || tree == null)
                {
                    if (tree == null && !report.ImportFailed)
                        report.Add(Bucket.Import, originalRoot.Kind().ToString(), "importer produced no tree");
                    result.Failures = report.Failures;
                    return result;
                }
                tree.WriteOutput(context);
            }
            emitted = context.Output();
        }
        catch (Exception e)
        {
            report.Add(Bucket.Import, originalRoot.Kind().ToString(), e.GetType().Name + ": " + Trim(e.Message));
            result.Failures = report.Failures;
            return result;
        }

        result.Emitted = emitted;

        // 2. re-parse ----------------------------------------------------------------
        var reparsed = CSharpSyntaxTree.ParseText(SourceText.From(emitted), options);
        var reparsedRoot = reparsed.GetRoot();
        var errors = reparsed.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
        {
            foreach (var d in errors.Take(6))
            {
                var token = reparsedRoot.FindToken(Math.Min(d.Location.SourceSpan.Start, Math.Max(0, emitted.Length - 1)));
                var site = token.Parent?.Kind().ToString() ?? "CompilationUnit";
                report.Add(Bucket.Reparse, site, $"{d.Id} {Trim(d.GetMessage())}");
            }
            result.Failures = report.Failures;
            return result;
        }

        // 3. compare -----------------------------------------------------------------
        if (Compare.Equivalent(originalRoot, reparsedRoot))
        {
            result.Passed = true;
            result.Failures = report.Failures;
            return result;
        }

        result.CrossCheckOnly = Compare.TokenAndKindEquivalent(originalRoot, reparsedRoot);
        Compare.Diff(originalRoot, reparsedRoot, report);
        result.Failures = report.Failures;
        return result;
    }

    private static string Trim(string s)
    {
        s = s.Replace("\r", " ").Replace("\n", " ");
        return s.Length > 110 ? s.Substring(0, 110) + "..." : s;
    }

    // ---------------------------------------------------------------- reporting

    private static void PrintSet(Action<string> emit, LayerKind layer, string name,
                                 List<FileResult> results, int dumpFirst)
    {
        var attempted = results.Count(r => !r.SourceRejected);
        var passed = results.Count(r => r.Passed);
        var rejected = results.Count(r => r.SourceRejected);
        var pct = attempted == 0 ? 0 : 100.0 * passed / attempted;

        emit("--------------------------------------------------------------------------------");
        emit($"LAYER {layer.ToString().ToUpperInvariant()}   CORPUS {name}");
        emit("--------------------------------------------------------------------------------");
        emit($"  files round-tripping / files attempted : {passed} / {attempted}   ({pct:F1}%)");
        var cross = results.Count(r => r.CrossCheckOnly);
        if (cross > 0)
            emit($"  cross-check verdict (same node kinds + same tokens) : {passed + cross} / {attempted}" +
                 $"   ({100.0 * (passed + cross) / attempted:F1}%)   [{cross} files IsEquivalentTo rejects and the cross-check does not]");
        if (rejected > 0)
            emit($"  excluded (source does not parse at C# 13) : {rejected}   [{results.Count} files on disk]");

        var byBucket = new Dictionary<Bucket, Dictionary<string, (int files, string reason)>>();
        var bucketFiles = new Dictionary<Bucket, int>();
        foreach (var r in results)
        {
            if (r.Passed || r.SourceRejected) continue;
            foreach (var b in r.Failures.Select(f => f.Bucket).Distinct())
                bucketFiles[b] = bucketFiles.GetValueOrDefault(b) + 1;

            foreach (var group in r.Failures.GroupBy(f => (f.Bucket, f.Kind)))
            {
                if (!byBucket.TryGetValue(group.Key.Bucket, out var map))
                    byBucket[group.Key.Bucket] = map = new Dictionary<string, (int, string)>();
                var existing = map.GetValueOrDefault(group.Key.Kind);
                map[group.Key.Kind] = (existing.files + 1,
                    existing.reason ?? group.First().Reason);
            }
        }

        emit("");
        emit("  failure histogram - files affected, by node kind, per bucket");
        foreach (var bucket in new[] { Bucket.Import, Bucket.Reparse, Bucket.Structure })
        {
            var label = bucket switch
            {
                Bucket.Import => "(a) importer could not build a tree for this node kind",
                Bucket.Reparse => "(b) tree built, emitted text does not re-parse",
                _ => "(c) re-parsed tree differs structurally from the original",
            };
            emit($"    {label}   [{bucketFiles.GetValueOrDefault(bucket)} files]");
            if (!byBucket.TryGetValue(bucket, out var map) || map.Count == 0)
            {
                emit("        (none)");
                continue;
            }
            foreach (var kv in map.OrderByDescending(k => k.Value.files).ThenBy(k => k.Key, StringComparer.Ordinal).Take(30))
                emit($"        {kv.Value.files,6}  {kv.Key,-42} {kv.Value.reason}");
            if (map.Count > 30) emit($"        ... {map.Count - 30} more kinds");
        }

        if (dumpFirst > 0)
        {
            emit("");
            emit("  first failing files:");
            foreach (var r in results.Where(x => !x.Passed && !x.SourceRejected).Take(dumpFirst))
            {
                emit($"    {r.Path}");
                foreach (var f in r.Failures.Take(6)) emit($"        {f}");
                if (r.Emitted != null)
                    emit("        emitted: " + Trim(r.Emitted.Length > 400 ? r.Emitted.Substring(0, 400) : r.Emitted));
            }
        }
        emit("");
    }
}
