using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

// CSharpAuthor has its own LanguageVersion (the profiles capability enum). It used to sit in the
// bare CSharpAuthor namespace, and this file sits in a namespace nested inside that one, where
// enclosing-namespace members beat using-aliases - so no alias could win and the distinct name was
// the only way out. The enum now lives in CSharpAuthor.Profiles, which this file does not import,
// so the shadowing is gone; the alias is kept because naming which LanguageVersion is meant reads
// better in a file that is entirely about Roslyn's.
using RoslynLangVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// Compiles emitted C# and fails the test if the compiler rejects it.
/// </summary>
/// <remarks>
/// <para>
/// The adversary's whole value rests on this type. An assertion that compares the emitted string to
/// an expected string only ever proves the emitter still does what it did yesterday - if yesterday's
/// output did not compile, the test locks the defect in. Handing the string to Roslyn asks the only
/// question that matters, and it cannot be answered wrongly by agreement between a test and the code
/// under test.
/// </para>
/// <para>
/// Every reference assembly loaded into this test process is passed to the compilation, so an emitted
/// file may name anything the tests themselves can name.
/// </para>
/// </remarks>
internal static class RoslynAssert
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> References =
        new(LoadReferences);

    /// <summary>
    /// The highest version this Roslyn understands. 4.14 tops out at C# 13 - handoff §4 - so a
    /// construct newer than that cannot be validated here, only asserted as a string.
    /// </summary>
    public const RoslynLangVersion MaxLanguageVersion = RoslynLangVersion.CSharp13;

    /// <summary>
    /// Parses and compiles <paramref name="source"/> as a whole file, asserting the compiler reports
    /// no errors. Warnings are allowed through; the question is whether the code is legal C#.
    /// </summary>
    public static void Compiles(
        string source,
        RoslynLangVersion languageVersion = MaxLanguageVersion,
        bool allowUnsafe = true,
        params string[] warningsAsErrors)
    {
        var diagnostics = Diagnose(source, languageVersion, allowUnsafe, warningsAsErrors);

        var errors = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (errors.Count == 0)
        {
            return;
        }

        Assert.True(false, BuildFailureMessage(source, errors));
    }

    /// <summary>
    /// Compiles a member declaration by placing it in a class, for output that is not a whole file.
    /// </summary>
    public static void MemberCompiles(
        string member,
        string? containerHeader = null,
        string preamble = "",
        RoslynLangVersion languageVersion = MaxLanguageVersion,
        params string[] warningsAsErrors)
    {
        var header = containerHeader ?? "public class AdversaryHost";

        var source = new StringBuilder();

        source.AppendLine("#nullable enable");
        source.AppendLine("using System;");
        source.AppendLine("using System.Collections;");
        source.AppendLine("using System.Collections.Generic;");
        source.AppendLine("using System.Collections.Immutable;");
        source.AppendLine("using System.Linq;");
        source.AppendLine("using System.Threading.Tasks;");
        source.AppendLine(preamble);
        source.AppendLine(header);
        source.AppendLine("{");
        source.AppendLine(member);
        source.AppendLine("}");

        Compiles(source.ToString(), languageVersion, allowUnsafe: true,
            warningsAsErrors: warningsAsErrors);
    }

    /// <summary>
    /// Compiles a statement by placing it in a method body.
    /// </summary>
    public static void StatementCompiles(
        string statement,
        string preamble = "",
        RoslynLangVersion languageVersion = MaxLanguageVersion,
        params string[] warningsAsErrors)
    {
        MemberCompiles(
            "public void AdversaryMethod()\n{\n" + statement + "\n}",
            preamble: preamble,
            languageVersion: languageVersion,
            warningsAsErrors: warningsAsErrors);
    }

    /// <summary>
    /// Compiles an expression by assigning it to a discard, so an expression that parses but is not
    /// a value is still rejected.
    /// </summary>
    public static void ExpressionCompiles(
        string expression,
        string preamble = "",
        RoslynLangVersion languageVersion = MaxLanguageVersion,
        params string[] warningsAsErrors)
    {
        StatementCompiles("_ = " + expression + ";", preamble, languageVersion, warningsAsErrors);
    }

    /// <summary>
    /// The compiler's errors for <paramref name="source"/>, for a test that wants to name the exact
    /// diagnostic a defect produces rather than merely assert failure.
    /// </summary>
    public static IReadOnlyList<Diagnostic> Errors(
        string source,
        RoslynLangVersion languageVersion = MaxLanguageVersion,
        params string[] warningsAsErrors) =>
        Diagnose(source, languageVersion, allowUnsafe: true, warningsAsErrors)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

    /// <summary>
    /// The parse and compile diagnostics, in that order - a parse error usually explains every
    /// binding error after it, so reporting both together reads better than either alone.
    /// </summary>
    /// <remarks>
    /// <paramref name="warningsAsErrors"/> is how a nullability question becomes a compile question.
    /// <c>string[]?</c> and <c>string?[]</c> both compile, so a plain compile check cannot tell them
    /// apart; assigning null to an element and promoting CS8625 to an error can.
    /// </remarks>
    private static IReadOnlyList<Diagnostic> Diagnose(
        string source, RoslynLangVersion languageVersion, bool allowUnsafe,
        string[]? warningsAsErrors = null)
    {
        // DocumentationMode.Diagnose is what makes CS1570 - "XML comment has badly formed XML" -
        // appear at all. Without it a malformed documentation comment is parsed as ordinary trivia
        // and no test could ever see the defect.
        var tree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(languageVersion, DocumentationMode.Diagnose));

        var options = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            allowUnsafe: allowUnsafe,
            nullableContextOptions: NullableContextOptions.Enable);

        if (warningsAsErrors is { Length: > 0 })
        {
            options = options.WithSpecificDiagnosticOptions(
                warningsAsErrors.ToDictionary(id => id, _ => ReportDiagnostic.Error));
        }

        var compilation = CSharpCompilation.Create(
            "AdversaryAssembly",
            new[] { tree },
            References.Value,
            options);

        var result = new List<Diagnostic>();

        result.AddRange(tree.GetDiagnostics());
        result.AddRange(compilation.GetDiagnostics());

        return result;
    }

    private static string BuildFailureMessage(string source, IReadOnlyList<Diagnostic> errors)
    {
        var message = new StringBuilder();

        message.AppendLine("Emitted code does not compile:");

        foreach (var error in errors.Take(12))
        {
            var line = error.Location.GetLineSpan().StartLinePosition.Line + 1;

            message.Append("  line ").Append(line).Append(": ")
                .Append(error.Id).Append(' ').AppendLine(error.GetMessage());
        }

        if (errors.Count > 12)
        {
            message.Append("  ... ").Append(errors.Count - 12).AppendLine(" more");
        }

        message.AppendLine("--- source ---");

        var sourceLines = source.Replace("\r\n", "\n").Split('\n');

        for (var i = 0; i < sourceLines.Length; i++)
        {
            message.Append((i + 1).ToString().PadLeft(4)).Append("| ").AppendLine(sourceLines[i]);
        }

        return message.ToString();
    }

    /// <summary>
    /// Every assembly the test host already trusts. Reading the runtime's own list rather than
    /// naming assemblies keeps this working across SDK versions.
    /// </summary>
    private static ImmutableArray<MetadataReference> LoadReferences()
    {
        var builder = ImmutableArray.CreateBuilder<MetadataReference>();

        var trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;

        if (!string.IsNullOrEmpty(trusted))
        {
            foreach (var path in trusted!.Split(Path.PathSeparator))
            {
                if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                {
                    builder.Add(MetadataReference.CreateFromFile(path));
                }
            }
        }

        return builder.ToImmutable();
    }
}
