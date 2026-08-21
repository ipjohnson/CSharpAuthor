namespace CSharpAuthor.Profiles;

/// <summary>
/// An output context that carries the profile a write is running under.
/// </summary>
/// <remarks>
/// The profile reaches a node through the context rather than through the tree, because the tree
/// is version-agnostic and the same tree has to be renderable twice. Any context can opt in by
/// implementing this - which is how <c>proto/deferred/DeferredContext.cs</c> will carry a profile
/// once it lands, without <see cref="IOutputContext"/> itself changing shape and breaking every
/// consumer that implements it.
/// </remarks>
#if CSHARPAUTHOR_PUBLIC_API
public
#endif
interface IProfiledOutputContext : IOutputContext
{
    /// <summary>The profile in force, and everything this write has decided so far.</summary>
    EmitSession Session { get; }
}

/// <summary>
/// V1's <see cref="OutputContext"/> with a profile attached.
/// </summary>
/// <remarks>
/// Every existing component writes into it unmodified. Components that ask the session get
/// version-appropriate output; components that do not are unaffected, which is what makes
/// adopting profiles incremental rather than a rewrite.
/// </remarks>
#if CSHARPAUTHOR_PUBLIC_API
public
#endif
class ProfiledOutputContext : OutputContext, IProfiledOutputContext
{
    /// <summary>Starts a write under a profile.</summary>
    public ProfiledOutputContext(EmitProfile? profile = null)
        : this(new EmitSession(profile))
    {
    }

    /// <summary>Continues a write under an existing session, sharing its diagnostics.</summary>
    public ProfiledOutputContext(EmitSession session)
        : base(OptionsFor(session))
    {
        Session = session;
    }

    private static OutputContextOptions OptionsFor(EmitSession session) =>
        session == null
            ? throw new System.ArgumentNullException(nameof(session))
            : session.Profile.ToOutputContextOptions();

    /// <inheritdoc />
    public EmitSession Session { get; }

    /// <summary>The profile this context renders for.</summary>
    public EmitProfile Profile => Session.Profile;
}

/// <summary>
/// Reaching the profile from inside a writer.
/// </summary>
#if CSHARPAUTHOR_PUBLIC_API
public
#endif
static class OutputContextProfileExtensions
{
    /// <summary>
    /// The session for this write, or <see cref="EmitSession.Discarding"/> when the context
    /// carries none - which is V1's behaviour, so a context that knows nothing about profiles
    /// keeps emitting exactly what it emitted before.
    /// </summary>
    public static EmitSession EmitSession(this IOutputContext outputContext) =>
        CSharpAuthor.Profiles.EmitSession.For(outputContext);

    /// <summary>The profile for this write.</summary>
    public static EmitProfile EmitProfile(this IOutputContext outputContext) =>
        CSharpAuthor.Profiles.EmitSession.For(outputContext).Profile;
}
