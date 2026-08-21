using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using RoslynLanguageVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion;

namespace CSharpAuthor.Tests.ProfileTests;

/// <summary>
/// One tree, compiled clean at C# 8 and at C# 12.
/// </summary>
/// <remarks>
/// <para>
/// <c>proto/downlevel/DownlevelDemo.cs</c> as a test. The claim it exists to check is the one
/// thing a tree can do that a StringBuilder cannot: the same nodes, built once, rendered twice,
/// and handed to a real compiler at both versions. Nothing here compares output against a string
/// somebody typed - the oracle is Roslyn.
/// </para>
/// <para>
/// <strong>What this cannot check.</strong> <c>Microsoft.CodeAnalysis.CSharp</c> 4.14.0 knows
/// language versions only up to C# 13, so the C# 15 rendering of a labeled jump is asserted to be
/// <em>unparseable here</em> rather than correct. See
/// <see cref="TheParserCannotCheckWhatItDoesNotKnow"/>.
/// </para>
/// </remarks>
public class DownlevelDemoTests
{
    [Fact]
    public void TheSameTreeCompilesAtCSharp12()
    {
        var result = ProfileEmitter.Emit(Widget(), EmitProfile.Default);

        AssertCompiles(result.Code, RoslynLanguageVersion.CSharp12);

        AssertEqual.ContainsWithoutNewLine("namespace Gen;", result.Code);
        AssertEqual.ContainsWithoutNewLine("public string Name { get; init; }", result.Code);
        AssertEqual.ContainsWithoutNewLine("[1, 2, 3]", result.Code);
        AssertEqual.ContainsWithoutNewLine("nameof(Widget)", result.Code);
        AssertEqual.ContainsWithoutNewLine("= new();", result.Code);
        AssertEqual.ContainsWithoutNewLine("switch {", result.Code);

        Assert.False(result.HasErrors);
        Assert.Empty(result.DownlevelNotes);
    }

    [Fact]
    public void TheSameTreeCompilesAtCSharp8()
    {
        var result = ProfileEmitter.Emit(Widget(), EmitProfile.Conservative);

        AssertCompiles(result.Code, RoslynLanguageVersion.CSharp8);

        AssertEqual.ContainsWithoutNewLine("namespace Gen\n{", result.Code);
        AssertEqual.ContainsWithoutNewLine("public string Name { get; set; }", result.Code);
        AssertEqual.ContainsWithoutNewLine("new int[] { 1, 2, 3 }", result.Code);
        AssertEqual.ContainsWithoutNewLine("new StringBuilder()", result.Code);

        // The one thing that is not the same on the way down says so, in the file.
        Assert.Contains(
            "// DOWNLEVEL: Name: 'init' unavailable below C#9 — emitted as a settable property, immutability lost",
            result.Code);

        Assert.False(result.HasErrors);
    }

    [Fact]
    public void TheTreeIsBuiltOnceAndRenderedTwice()
    {
        // Not two trees that happen to agree: one tree, two profiles. If a node had committed
        // itself to text when it was built, the second rendering would be the first one again.
        var tree = Widget();

        var modern = ProfileEmitter.Emit(tree, EmitProfile.Default);
        var old = ProfileEmitter.Emit(tree, EmitProfile.Conservative);

        AssertCompiles(modern.Code, RoslynLanguageVersion.CSharp12);
        AssertCompiles(old.Code, RoslynLanguageVersion.CSharp8);

        Assert.NotEqual(modern.Code, old.Code);

        // And rendering it a third time gives the first answer back.
        Assert.Equal(modern.Code, ProfileEmitter.Emit(tree, EmitProfile.Default).Code);
    }

    [Fact]
    public void TheCSharp12RenderingDoesNotCompileAtCSharp8()
    {
        // The failure case from the prototype: the point of choosing per target is that the
        // choice matters.
        var modern = ProfileEmitter.Emit(Widget(), EmitProfile.Default).Code;

        var errors = Compile(modern, RoslynLanguageVersion.CSharp8);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void TheParserCannotCheckWhatItDoesNotKnow()
    {
        // Microsoft.CodeAnalysis.CSharp 4.14.0 knows language versions only up to C# 13. Its
        // Preview cannot parse `break outer;` even though the .NET 11 SDK compiler can. The
        // library will happily render for C# 15; nothing in this repository can validate it, and
        // saying so is the honest version of a conformance claim.
        Assert.Equal(
            LanguageVersion.CSharp13,
            CSharpAuthor.Roslyn.EmitProfileRoslynExtensions.LatestSupported());

        var labelled = ProfileEmitter.Emit(Widget(), EmitProfile.Latest).Code;

        Assert.Contains("break outer;", labelled);

        var errors = Compile(labelled, RoslynLanguageVersion.Preview);

        Assert.NotEmpty(errors);

        // ... and the same tree rendered for a version this parser does know is fine.
        AssertCompiles(ProfileEmitter.Emit(Widget(), EmitProfile.Default).Code, RoslynLanguageVersion.CSharp12);
    }

    [Fact]
    public void ARequiredMemberCompilesWithItsSupportTypes()
    {
        // C# 11 on a framework that predates RequiredMemberAttribute: the polyfills are what make
        // the difference between output that compiles and output that does not.
        var file = new CSharpFileDefinition("Gen");
        var definition = file.AddClass("Widget");

        definition.Modifiers = ComponentModifier.Public;
        definition.AddProperty(typeof(string), "Name").IsRequired = true;

        var result = ProfileEmitter.Emit(
            file,
            EmitProfile.Default.With(p =>
            {
                p.Target = LanguageVersion.CSharp11;
                p.Polyfills = PolyfillMode.Always;
            }));

        AssertCompiles(result.Code, RoslynLanguageVersion.CSharp11);
        AssertEqual.ContainsWithoutNewLine("public required string Name", result.Code);
    }

    [Fact]
    public void EveryTargetFromCSharp8UpwardsProducesSomethingThatCompiles()
    {
        var versions = new[]
        {
            (LanguageVersion.CSharp8, RoslynLanguageVersion.CSharp8),
            (LanguageVersion.CSharp9, RoslynLanguageVersion.CSharp9),
            (LanguageVersion.CSharp10, RoslynLanguageVersion.CSharp10),
            (LanguageVersion.CSharp11, RoslynLanguageVersion.CSharp11),
            (LanguageVersion.CSharp12, RoslynLanguageVersion.CSharp12),
            (LanguageVersion.CSharp13, RoslynLanguageVersion.CSharp13)
        };

        var tree = Widget();

        foreach (var (target, parsed) in versions)
        {
            var profile = EmitProfile.Default.With(p =>
            {
                p.Target = target;
                p.Polyfills = PolyfillMode.Always;
                p.PreferRawStrings = true;
            });

            var result = ProfileEmitter.Emit(tree, profile);

            AssertCompiles(result.Code, parsed);
        }
    }

    /// <summary>
    /// The prototype's tree, built with the library rather than with a demo's own node types.
    /// </summary>
    private static CSharpFileDefinition Widget()
    {
        var file = new CSharpFileDefinition("Gen");
        var widget = file.AddClass("Widget");

        widget.Modifiers = ComponentModifier.Public;

        var name = widget.AddProperty(typeof(string), "Name");

        name.Set!.IsInit = true;

        var sizes = widget.AddField(typeof(int[]), "Sizes");

        sizes.Modifiers = ComponentModifier.Public;
        sizes.InitializeValue = CollectionExpressionStatement.Of(TypeDefinition.Get(typeof(int)), 1, 2, 3);

        var banner = widget.AddField(typeof(string), "Banner");

        banner.Modifiers = ComponentModifier.Public;
        banner.InitializeValue = new StringLiteralStatement("he said \"hi\" loudly");

        var which = widget.AddField(typeof(string), "Which");

        which.Modifiers = ComponentModifier.Public;
        which.InitializeValue = new NameOfStatement("Widget");

        var buffer = widget.AddField(typeof(StringBuilder), "Buf");

        buffer.Modifiers = ComponentModifier.Public;
        buffer.InitializeValue = TargetTypedNewStatement.Of(TypeDefinition.Get(typeof(StringBuilder)));

        var describe = widget.AddMethod("Describe");

        describe.Modifiers = ComponentModifier.Public;
        describe.SetReturnType(typeof(string));
        describe.AddParameter(typeof(int), "n");
        describe.Return(
            new SwitchExpressionStatement("n")
                .AddArm("1", "\"one\"")
                .AddArm("2", "\"two\"")
                .Otherwise("null"));

        var scan = widget.AddMethod("Scan");

        scan.Modifiers = ComponentModifier.Public;

        // A single-rank array on purpose. A jagged one renders as `Int32[][][]` today - an extra
        // rank and the BCL name rather than the keyword - which is the type model's known defect,
        // not this slice's, and not something to work around silently in library code.
        scan.AddParameter(typeof(int[]), "values");

        var outer = new LabeledLoopStatement("outer", "foreach (var row in values)");
        var inner = new LabeledLoopStatement("inner", "foreach (var v in values)");

        inner.If("v < 0").Add(new LabeledJumpStatement(LabeledJumpKind.Break, "outer"));
        outer.Add(inner);
        scan.Add(outer);

        return file;
    }

    private static void AssertCompiles(string code, RoslynLanguageVersion version)
    {
        var errors = Compile(code, version);

        Assert.True(
            errors.Count == 0,
            "Rendered for " + version + " but the compiler disagrees:" + Environment.NewLine +
            string.Join(Environment.NewLine, errors) + Environment.NewLine + code);
    }

    private static IReadOnlyList<string> Compile(string code, RoslynLanguageVersion version)
    {
        var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(version));

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location));

        var compilation = CSharpCompilation.Create(
            "downlevel",
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.Id + ": " + d.GetMessage() + " @ " + d.Location.GetLineSpan())
            .ToList();
    }
}
