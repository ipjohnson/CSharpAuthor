using System;
using System.Collections.Generic;

namespace CSharpAuthor.Profiles;

/// <summary>
/// The text one tree produced for one profile, and everything the writer decided to produce it.
/// </summary>
/// <remarks>
/// The second reason this namespace is not the bare <c>CSharpAuthor</c>: <c>EmitResult</c> is also
/// the name Roslyn gives what <c>Compilation.Emit</c> returns
/// (<c>Microsoft.CodeAnalysis.Emit.EmitResult</c>). A generator's test harness imports that
/// namespace routinely - DependencyModules' <c>GeneratedAssembly.cs</c> does - and in the bare
/// namespace this name would collide there the moment the file also said
/// <c>using CSharpAuthor;</c>. See the note above the namespace declaration in
/// <c>Profiles/LanguageVersion.cs</c>.
/// </remarks>
#if CSHARPAUTHOR_PUBLIC_API
public
#endif
sealed class EmitResult
{
    internal EmitResult(string code, EmitSession session)
    {
        Code = code;
        Session = session;
    }

    /// <summary>The file.</summary>
    public string Code { get; }

    /// <summary>The session that produced it.</summary>
    public EmitSession Session { get; }

    /// <summary>Everything recorded while producing it.</summary>
    public IReadOnlyList<EmitDiagnostic> Diagnostics => Session.Diagnostics;

    /// <summary>Whether something could not be emitted.</summary>
    public bool HasErrors => Session.HasErrors;

    /// <summary>The <c>// DOWNLEVEL:</c> comments this rendering produced.</summary>
    public IReadOnlyList<string> DownlevelNotes => Session.DownlevelNotes;

    /// <summary>Support types emitted alongside the code.</summary>
    public IReadOnlyList<PolyfillType> Polyfills => Session.RequiredPolyfills;

    /// <inheritdoc />
    public override string ToString() => Code;
}

/// <summary>
/// Renders a tree for a profile.
/// </summary>
/// <remarks>
/// The same tree, twice, gives two different files - that is the whole claim, and
/// <c>ProfileTests/DownlevelDemoTests</c> is where it is checked against a real compiler at two
/// language versions.
/// </remarks>
#if CSHARPAUTHOR_PUBLIC_API
public
#endif
static class ProfileEmitter
{
    /// <summary>
    /// Renders a file.
    /// </summary>
    /// <remarks>
    /// The profile decides whether the namespace is file-scoped, overriding whatever the tree was
    /// built with: which of the two forms to write is a rendering choice, and rendering choices
    /// live on the profile. The tree is left exactly as it was found.
    /// </remarks>
    public static EmitResult Emit(CSharpFileDefinition file, EmitProfile? profile = null)
    {
        if (file == null)
        {
            throw new ArgumentNullException(nameof(file));
        }

        var result = Render(file, profile ?? EmitProfile.Default, forceBlockNamespace: false);

        if (result.Polyfills.Count == 0 || !result.Session.Profile.CanEmit(LanguageFeature.FileScopedNamespaces))
        {
            return result;
        }

        // C# will not accept a file-scoped namespace beside any other namespace declaration, and a
        // support type has to declare System.Runtime.CompilerServices. The file-scoped form is a
        // preference; compiling is not. Render it again as a block.
        var blockScoped = Render(file, result.Session.Profile, forceBlockNamespace: true);

        blockScoped.Session.Report(new EmitDiagnostic(
            EmitDiagnostic.PreferenceResolvedId,
            EmitSeverity.Info,
            LanguageFeature.FileScopedNamespaces,
            LanguageFeatures.MinimumVersion(LanguageFeature.FileScopedNamespaces),
            result.Session.Profile.EffectiveTarget,
            null,
            "block namespace emitted because the file also declares a support type, and C# forbids " +
            "a file-scoped namespace beside another namespace declaration."));

        return blockScoped;
    }

    /// <summary>
    /// Renders a single component - a member, a statement, an expression - for a profile.
    /// </summary>
    public static EmitResult Emit(IOutputComponent component, EmitProfile? profile = null)
    {
        if (component == null)
        {
            throw new ArgumentNullException(nameof(component));
        }

        var session = new EmitSession(profile);
        var context = new ProfiledOutputContext(session);

        component.WriteOutput(context);

        return Compose(session, context.Output());
    }

    private static EmitResult Render(CSharpFileDefinition file, EmitProfile profile, bool forceBlockNamespace)
    {
        var session = new EmitSession(profile);

        var fileScoped = session.MayEmit(LanguageFeature.FileScopedNamespaces, "namespace") &&
                         !forceBlockNamespace;

        var previous = file.FileScopedNamespace;

        file.FileScopedNamespace = fileScoped;

        var context = new ProfiledOutputContext(session);

        try
        {
            file.WriteOutput(context);
        }
        finally
        {
            file.FileScopedNamespace = previous;
        }

        return Compose(session, context.Output());
    }

    private static EmitResult Compose(EmitSession session, string body)
    {
        var header = Write(session, session.WriteDownlevelHeader);
        var footer = Write(session, session.WritePolyfills);

        return new EmitResult(header + body + footer, session);
    }

    private static string Write(EmitSession session, Action<IOutputContext> write)
    {
        var context = new ProfiledOutputContext(session);

        write(context);

        return context.Output();
    }
}
