using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace RoslynProbe;

internal static class Program
{
    private static int Main(string[] args)
    {
        var asm = typeof(Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree).Assembly;

        var names = asm.GetTypes()
            .Where(t => t.IsPublic
                        && !t.IsAbstract
                        && t.Namespace == "Microsoft.CodeAnalysis.CSharp.Syntax"
                        && t.Name.EndsWith("Syntax", StringComparison.Ordinal))
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var version = asm.GetName().Version?.ToString() ?? "unknown";
        var informational = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                            ?? version;

        // The parser's own ceiling, measured rather than assumed: the highest named
        // LanguageVersion the referenced Roslyn declares.
        var langNames = Enum.GetNames(typeof(Microsoft.CodeAnalysis.CSharp.LanguageVersion))
            .Where(n => n.StartsWith("CSharp", StringComparison.Ordinal))
            .OrderBy(n => (int)Enum.Parse(typeof(Microsoft.CodeAnalysis.CSharp.LanguageVersion), n))
            .ToList();

        // Syntax.xml is newer than the parser package, so some grammar FIELDS are missing
        // too (labeled break/continue's Name, for one). Record every property as well.
        var members = asm.GetTypes()
            .Where(t => t.IsPublic && t.Namespace == "Microsoft.CodeAnalysis.CSharp.Syntax"
                        && t.Name.EndsWith("Syntax", StringComparison.Ordinal))
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                              .Select(p => t.Name + "." + p.Name))
            .Distinct()
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var outPath = args.Length > 0 ? args[0] : null;
        if (outPath != null)
        {
            File.WriteAllLines(outPath, names);
            File.WriteAllLines(Path.ChangeExtension(outPath, ".members.txt"), members);
            File.WriteAllText(outPath + ".meta",
                $"assembly={asm.GetName().Name}\nversion={version}\ninformational={informational}\n" +
                $"syntaxTypes={names.Count}\nmaxLanguageVersion={langNames.LastOrDefault() ?? "?"}\n" +
                $"languageVersions={string.Join(",", langNames)}\n");
        }

        Console.WriteLine($"roslyn={informational} types={names.Count} maxLanguageVersion={langNames.LastOrDefault()}");
        return 0;
    }
}
