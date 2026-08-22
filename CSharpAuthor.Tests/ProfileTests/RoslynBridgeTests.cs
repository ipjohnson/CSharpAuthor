using System.Collections.Generic;
using CSharpAuthor.Profiles;
using CSharpAuthor.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;
using EmitLanguageVersion = CSharpAuthor.Profiles.LanguageVersion;
using RoslynLanguageVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion;

namespace CSharpAuthor.Tests.ProfileTests;

/// <summary>
/// The Roslyn-gated bridge: the host project's formatting from its .editorconfig, and its
/// language version from its parse options.
/// </summary>
/// <remarks>
/// The file under test is compiled into this assembly by a link, not referenced: it is excluded
/// from the shipped library on purpose, so that netstandard2.0 build has no Roslyn dependency at
/// all.
/// </remarks>
public class RoslynBridgeTests
{
    [Fact]
    public void FormattingComesFromAnalyzerConfigOptions()
    {
        var options = new FakeOptions(new Dictionary<string, string>
        {
            { "indent_style", "tab" },
            { "indent_size", "4" },
            { "end_of_line", "crlf" },
            { "csharp_new_line_before_open_brace", "none" },
            { "csharp_style_namespace_declarations", "block_scoped:error" },
            { "csharp_style_var_when_type_is_apparent", "false:suggestion" }
        });

        var profile = options.ToEmitProfile();

        Assert.Equal('\t', profile.IndentChar);
        Assert.Equal(1, profile.IndentWidth);
        Assert.Equal("\r\n", profile.NewLine);
        Assert.Equal(BraceStyle.KAndR, profile.Braces);
        Assert.False(profile.FileScopedNamespace);
    }

    [Theory]
    [InlineData(RoslynLanguageVersion.CSharp8, EmitLanguageVersion.CSharp8)]
    [InlineData(RoslynLanguageVersion.CSharp9, EmitLanguageVersion.CSharp9)]
    [InlineData(RoslynLanguageVersion.CSharp10, EmitLanguageVersion.CSharp10)]
    [InlineData(RoslynLanguageVersion.CSharp11, EmitLanguageVersion.CSharp11)]
    [InlineData(RoslynLanguageVersion.CSharp12, EmitLanguageVersion.CSharp12)]
    [InlineData(RoslynLanguageVersion.CSharp13, EmitLanguageVersion.CSharp13)]
    [InlineData(RoslynLanguageVersion.CSharp7_3, EmitLanguageVersion.CSharp7_3)]
    public void TheVersionComesFromTheParseOptions(RoslynLanguageVersion parsed, EmitLanguageVersion expected)
    {
        var profile = EmitProfile.Default.WithTargetFrom(new CSharpParseOptions(parsed));

        Assert.Equal(expected, profile.Target);
    }

    [Fact]
    public void LatestAndDefaultAreResolvedToWhatTheCompilerMeansByThem()
    {
        // Not carried through as a sentinel: a profile that says "latest" cannot be compared
        // against a minimum version, and comparing is the whole job.
        Assert.Equal(
            EmitLanguageVersion.CSharp13,
            EmitProfile.Default.WithTargetFrom(new CSharpParseOptions(RoslynLanguageVersion.Latest)).Target);

        Assert.Equal(
            EmitProfileRoslynExtensions.LatestSupported(),
            EmitProfile.Default.WithTargetFrom(new CSharpParseOptions(RoslynLanguageVersion.Default)).Target);
    }

    [Fact]
    public void TheReferencedCompilerIsWhatDecidesWhatLatestMeans()
    {
        // Microsoft.CodeAnalysis.CSharp 4.14.0 knows language versions only up to C# 13. This
        // number is measured from the package this test compiles against, not asserted from the
        // SDK installed on the machine - and if it ever changes, the claim in the profile
        // documentation has to change with it.
        Assert.Equal(EmitLanguageVersion.CSharp13, EmitProfileRoslynExtensions.LatestSupported());
        Assert.Equal(EmitLanguageVersion.CSharp13, EmitProfileRoslynExtensions.LatestSupportedProfile().Target);
    }

    [Fact]
    public void AnythingThatIsNotCSharpParseOptionsLeavesTheTargetAlone()
    {
        var profile = EmitProfile.Default.With(p => p.Target = EmitLanguageVersion.CSharp9);

        Assert.Equal(EmitLanguageVersion.CSharp9, profile.WithTargetFrom(null).Target);
    }

    [Fact]
    public void OneCallGivesAGeneratorBothHalves()
    {
        var profile = EmitProfileRoslynExtensions.ForGeneration(
            new FakeProvider(new FakeOptions(new Dictionary<string, string> { { "indent_size", "2" } })),
            new CSharpParseOptions(RoslynLanguageVersion.CSharp9));

        Assert.Equal(2, profile.IndentWidth);
        Assert.Equal(EmitLanguageVersion.CSharp9, profile.Target);
    }

    [Fact]
    public void AnEmitDiagnosticCanBeReportedOnTheCompilation()
    {
        // The other half of EmitErrorDirective: a generator collects rather than throws, and what
        // it collects belongs in the error list rather than in a comment nobody reads.
        var session = new EmitSession(
            EmitProfile.Conservative.With(
                p => p.OnCapabilityViolation = CapabilityViolationBehavior.EmitErrorDirective));

        session.Require(LanguageFeature.InlineArrays, "Buffer");

        var diagnostic = Assert.Single(session.Diagnostics).ToDiagnostic();

        Assert.Equal(EmitDiagnostic.CapabilityViolationId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("'inline array' on Buffer", diagnostic.GetMessage());
    }

    [Fact]
    public void ALossyDownlevelBecomesAWarningNotAnError()
    {
        var session = new EmitSession(EmitProfile.Conservative);

        session.MayEmit(LanguageFeature.InitOnlyProperties, "Name");

        var diagnostic = Assert.Single(session.Diagnostics).ToDiagnostic();

        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(EmitDiagnostic.LossyDownlevelId, diagnostic.Id);
    }

    private sealed class FakeOptions : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _values;

        public FakeOptions(Dictionary<string, string> values)
        {
            _values = values;
        }

        public override bool TryGetValue(string key, out string? value) => _values.TryGetValue(key, out value);
    }

    private sealed class FakeProvider : AnalyzerConfigOptionsProvider
    {
        public FakeProvider(AnalyzerConfigOptions options)
        {
            GlobalOptions = options;
        }

        public override AnalyzerConfigOptions GlobalOptions { get; }

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;
    }
}
