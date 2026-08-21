using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using CSharpAuthor.Roslyn;

namespace CSharpAuthor.Profiles;

/// <summary>
/// The two entry points from §4 that name Roslyn types, kept out of the core so that
/// netstandard2.0 build has no Roslyn reference at all.
/// </summary>
/// <remarks>
/// A partial rather than an extension class, because the handoff declares
/// <c>EmitProfile.FromEditorConfig(AnalyzerConfigOptions)</c> as a member. That makes this file
/// source-include-only: it is compiled into the consumer alongside the core when
/// <c>PackageCSharpAuthorIncludeSource</c> and <c>PackageCSharpAuthorIncludeRoslyn</c> are both
/// set, and cannot be added to the shipped assembly, which does not reference Roslyn.
/// </remarks>
public sealed partial class EmitProfile
{
    /// <summary>
    /// A profile that formats the way the host project's .editorconfig says to.
    /// </summary>
    public static EmitProfile FromEditorConfig(AnalyzerConfigOptions options) => options.ToEmitProfile();

    /// <summary>
    /// A profile targeting the language version the host project compiles at, formatted the way
    /// its .editorconfig says to.
    /// </summary>
    public static EmitProfile FromCompilation(
        AnalyzerConfigOptionsProvider? optionsProvider,
        ParseOptions? parseOptions) =>
        EmitProfileRoslynExtensions.ForGeneration(optionsProvider, parseOptions);
}
