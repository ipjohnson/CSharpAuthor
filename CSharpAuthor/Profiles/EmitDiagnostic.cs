using System;

namespace CSharpAuthor;

/// <summary>
/// How much a diagnostic matters. Only <see cref="Error"/> stops anything.
/// </summary>
public enum EmitSeverity
{
    /// <summary>A rendering choice was taken. Nothing changed meaning.</summary>
    Info,

    /// <summary>A downlevel was taken that does not mean quite the same thing.</summary>
    Warning,

    /// <summary>There was no way to emit what the tree asked for. Nothing correct could be written.</summary>
    Error
}

/// <summary>
/// What an <see cref="EmitSession"/> does when a feature with no downlevel is asked for below the
/// version that has it.
/// </summary>
/// <remarks>
/// There is deliberately no "carry on quietly" option. V1's defect class was silent wrongness -
/// output that looked fine and did not compile, or compiled and meant something else. Either the
/// caller finds out by exception, or the compiler finds out by <c>#error</c>.
/// </remarks>
public enum CapabilityViolationBehavior
{
    /// <summary>Throw <see cref="EmitCapabilityException"/> at the point of the violation.</summary>
    Throw,

    /// <summary>
    /// Record the diagnostic and write <c>#error</c> into the output, so the consumer's build
    /// fails with the reason in it. For a source generator, which cannot usefully throw.
    /// </summary>
    EmitErrorDirective
}

/// <summary>
/// Where a <c>// DOWNLEVEL:</c> comment goes.
/// </summary>
public enum DownlevelCommentPlacement
{
    /// <summary>Nowhere. The diagnostics are still recorded on the session.</summary>
    None,

    /// <summary>On the line above the member that was downlevelled.</summary>
    Inline,

    /// <summary>Collected at the top of the file, each naming its member.</summary>
    FileHeader
}

/// <summary>
/// When to emit a support type - <c>IsExternalInit</c>, <c>RequiredMemberAttribute</c> - beside
/// the code that needs it.
/// </summary>
/// <remarks>
/// Whether the support type is already there is a <em>target framework</em> question, and a
/// profile only knows the language version. <see cref="Auto"/> is therefore a proxy, not an
/// answer: a netstandard2.0 generator emitting C# 12 needs <see cref="Always"/>.
/// </remarks>
public enum PolyfillMode
{
    /// <summary>Never. The consumer is expected to have the types.</summary>
    None,

    /// <summary>
    /// When the target is the version that introduced the feature - a project pinned there is the
    /// one most likely to predate the support type.
    /// </summary>
    Auto,

    /// <summary>Whenever a feature that needs one is emitted.</summary>
    Always
}

/// <summary>
/// Something the writer decided, or could not do, while rendering a tree for a target version.
/// </summary>
public sealed class EmitDiagnostic
{
    /// <summary>A free downlevel was taken: a different rendering, the same meaning.</summary>
    public const string PreferenceResolvedId = "CSA0001";

    /// <summary>A downlevel was taken that changes meaning.</summary>
    public const string LossyDownlevelId = "CSA0002";

    /// <summary>A support type was emitted alongside the output.</summary>
    public const string PolyfillEmittedId = "CSA0003";

    /// <summary>The target does not have the feature and nothing else means the same thing.</summary>
    public const string CapabilityViolationId = "CSA1001";

    /// <summary>The caller ruled out the only downlevel this library could have taken.</summary>
    public const string NoDownlevelAvailableId = "CSA1002";

    internal EmitDiagnostic(
        string id,
        EmitSeverity severity,
        LanguageFeature feature,
        LanguageVersion requiredVersion,
        LanguageVersion target,
        string? context,
        string message)
    {
        Id = id;
        Severity = severity;
        Feature = feature;
        RequiredVersion = requiredVersion;
        Target = target;
        Context = context;
        Message = message;
    }

    /// <summary>The <c>CSA....</c> identifier.</summary>
    public string Id { get; }

    /// <summary>How much it matters.</summary>
    public EmitSeverity Severity { get; }

    /// <summary>The feature that was asked for.</summary>
    public LanguageFeature Feature { get; }

    /// <summary>The first version that has it.</summary>
    public LanguageVersion RequiredVersion { get; }

    /// <summary>The version being rendered for.</summary>
    public LanguageVersion Target { get; }

    /// <summary>The member or expression it happened in, where the writer knew one.</summary>
    public string? Context { get; }

    /// <summary>The sentence a human reads.</summary>
    public string Message { get; }

    /// <summary><c>CSA1001: ...</c>.</summary>
    public override string ToString() => Id + ": " + Message;
}

/// <summary>
/// Thrown when a tree asks for something the target language version cannot express and no
/// downlevel exists.
/// </summary>
/// <remarks>
/// The alternative would be to emit something that is not what the tree said - which is exactly
/// the failure mode V2 exists to remove. Set
/// <see cref="EmitProfile.OnCapabilityViolation"/> to
/// <see cref="CapabilityViolationBehavior.EmitErrorDirective"/> where throwing is not an option.
/// </remarks>
public sealed class EmitCapabilityException : InvalidOperationException
{
    internal EmitCapabilityException(EmitDiagnostic diagnostic)
        : base(diagnostic.ToString())
    {
        Diagnostic = diagnostic;
    }

    /// <summary>What could not be emitted.</summary>
    public EmitDiagnostic Diagnostic { get; }
}
