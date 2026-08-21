using System;
using CSharpAuthor.Profiles;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

// Both names are spelled through an alias, deliberately. `using CSharpAuthor.Profiles;` and
// `using Microsoft.CodeAnalysis.CSharp;` both contribute a `LanguageVersion`, so a bare mention of
// it here is CS0104 - and this file is the one place in the library that legitimately needs both.
// This is the same collision the profile enum was moved out of the bare `CSharpAuthor` namespace to
// spare consumers: down here an alias fixes it, because `CSharpAuthor.Roslyn` is not nested under
// `CSharpAuthor.Profiles` and neither name arrives as an enclosing-namespace member.
using EmitLanguageVersion = CSharpAuthor.Profiles.LanguageVersion;
using RoslynLanguageVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion;

namespace CSharpAuthor.Roslyn;

/// <summary>
/// The bridge between a compilation and an <see cref="EmitProfile"/>: the host project's
/// formatting from its .editorconfig, and its language version from its parse options.
/// </summary>
/// <remarks>
/// <para>
/// This folder is compiled into the consumer only when <c>PackageCSharpAuthorIncludeRoslyn</c> is
/// set. The core library targets netstandard2.0 and must not gain a Roslyn dependency; a
/// generator project already references Roslyn, so including this costs it nothing and adds no
/// package.
/// </para>
/// <para>
/// <strong>What this cannot tell you.</strong> The version this bridge reports is the version the
/// <em>referenced Roslyn</em> knows. <c>Microsoft.CodeAnalysis.CSharp</c> 4.14.0 stops at C# 13,
/// so <see cref="LatestSupported"/> returns C# 13 there even on an SDK whose compiler is newer,
/// and no parser in that package can validate a rendering above it. A profile can target
/// <see cref="EmitLanguageVersion.CSharp15"/>; nothing here can prove the output parses.
/// </para>
/// </remarks>
#if CSHARPAUTHOR_PUBLIC_API
public
#endif
static class EmitProfileRoslynExtensions
{
    /// <summary>
    /// A profile formatted the way this file's .editorconfig says to.
    /// </summary>
    public static EmitProfile ToEmitProfile(this AnalyzerConfigOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        return EmitProfile.FromEditorConfig(options.TryGetValue);
    }

    /// <summary>
    /// A profile for the options that apply to <paramref name="syntaxTree"/>, or the global ones
    /// when no tree is given - which is the usual case for a generated file, since it has no tree
    /// of its own yet.
    /// </summary>
    public static EmitProfile ToEmitProfile(this AnalyzerConfigOptionsProvider provider, SyntaxTree? syntaxTree = null)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        return (syntaxTree == null ? provider.GlobalOptions : provider.GetOptions(syntaxTree)).ToEmitProfile();
    }

    /// <summary>
    /// The same profile, targeting the language version the host project compiles at.
    /// </summary>
    /// <remarks>
    /// Anything that is not C# parse options leaves the target alone: the profile's own
    /// <see cref="EmitProfile.Target"/> is the fallback, exactly as the handoff specifies.
    /// </remarks>
    public static EmitProfile WithTargetFrom(this EmitProfile profile, ParseOptions? parseOptions)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        if (!(parseOptions is CSharpParseOptions csharp))
        {
            return profile;
        }

        return profile.With(p => p.Target = csharp.LanguageVersion.ToEmitVersion());
    }

    /// <summary>
    /// One profile for a generation pass: formatting from .editorconfig, version from the parse
    /// options.
    /// </summary>
    /// <example>
    /// <code>
    /// var profiles = context.AnalyzerConfigOptionsProvider
    ///     .Combine(context.ParseOptionsProvider)
    ///     .Select((pair, _) =&gt; EmitProfileRoslynExtensions.ForGeneration(pair.Left, pair.Right));
    /// </code>
    /// </example>
    public static EmitProfile ForGeneration(
        AnalyzerConfigOptionsProvider? optionsProvider,
        ParseOptions? parseOptions,
        SyntaxTree? syntaxTree = null)
    {
        var profile = optionsProvider == null
            ? EmitProfile.Default.Clone()
            : optionsProvider.ToEmitProfile(syntaxTree);

        return profile.WithTargetFrom(parseOptions);
    }

    /// <summary>
    /// Roslyn's language version as this library's.
    /// </summary>
    /// <remarks>
    /// <c>Default</c>, <c>Latest</c>, <c>LatestMajor</c> and <c>Preview</c> are resolved to the
    /// concrete version the referenced compiler means by them, so the profile carries a version it
    /// can actually compare against rather than a sentinel.
    /// </remarks>
    public static EmitLanguageVersion ToEmitVersion(this RoslynLanguageVersion version)
    {
        var value = (int)version.MapSpecifiedToEffectiveVersion();

        if (value <= 0)
        {
            return EmitLanguageVersion.Default;
        }

        // A compiler newer than this table would report a version it does not name. Treat it as
        // "everything", which is what it is, rather than inventing a number.
        return value > (int)EmitLanguageVersion.CSharp15
            ? EmitLanguageVersion.Latest
            : (EmitLanguageVersion)value;
    }

    /// <summary>
    /// The newest version the <em>referenced</em> Roslyn understands - C# 13 for
    /// <c>Microsoft.CodeAnalysis.CSharp</c> 4.14.0, whatever the installed SDK's compiler does.
    /// </summary>
    /// <remarks>
    /// This is what <see cref="EmitProfile.Latest"/> resolves to when there is a compiler to ask.
    /// Without one, <see cref="EmitLanguageVersion.Latest"/> means "assume it is there", which is
    /// a claim, not a measurement.
    /// </remarks>
    public static EmitLanguageVersion LatestSupported() => RoslynLanguageVersion.Latest.ToEmitVersion();

    /// <summary>
    /// An <see cref="EmitProfile.Latest"/> whose target is what the referenced compiler can
    /// actually parse.
    /// </summary>
    public static EmitProfile LatestSupportedProfile() =>
        EmitProfile.Latest.With(p => p.Target = LatestSupported());

    /// <summary>
    /// One emit diagnostic as a Roslyn one, so a generator can report it on the compilation
    /// instead of throwing.
    /// </summary>
    /// <remarks>
    /// This is the other half of <see cref="CapabilityViolationBehavior.EmitErrorDirective"/>: a
    /// generator collects rather than throws, and what it collects belongs in the error list with
    /// everything else rather than in a comment nobody reads.
    /// </remarks>
    public static Diagnostic ToDiagnostic(this EmitDiagnostic diagnostic, Location? location = null)
    {
        if (diagnostic == null)
        {
            throw new ArgumentNullException(nameof(diagnostic));
        }

        var severity = diagnostic.Severity == EmitSeverity.Error
            ? DiagnosticSeverity.Error
            : diagnostic.Severity == EmitSeverity.Warning
                ? DiagnosticSeverity.Warning
                : DiagnosticSeverity.Info;

        var descriptor = new DiagnosticDescriptor(
            diagnostic.Id,
            "CSharpAuthor " + diagnostic.Feature,
            "{0}",
            "CSharpAuthor",
            severity,
            isEnabledByDefault: true);

        return Diagnostic.Create(descriptor, location ?? Location.None, diagnostic.Message);
    }
}
