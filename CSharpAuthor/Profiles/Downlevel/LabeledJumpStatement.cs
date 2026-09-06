using System;

namespace CSharpAuthor.Profiles;

/// <summary>Which loop keyword a labeled jump is.</summary>
#if CSHARPAUTHOR_PUBLIC_API
public
#endif
enum LabeledJumpKind
{
    /// <summary>Leaves the labeled loop.</summary>
    Break,

    /// <summary>Starts the labeled loop's next iteration.</summary>
    Continue
}

/// <summary>
/// Leaving or continuing an outer loop by name, written as the <c>goto</c> that has meant this
/// since C# 1.
/// </summary>
/// <remarks>
/// <para>
/// There is no other form. C# has no labeled <c>break</c> or <c>continue</c> - <c>break outer;</c>
/// is not valid at any language version - so this always writes <c>goto outer_break;</c> and never
/// consults the profile about it.
/// </para>
/// <para>
/// The label a jump targets is declared by the <see cref="LabeledLoopStatement"/> it names, which
/// only declares the ones something actually jumps to - so this records the use rather than
/// assuming it.
/// </para>
/// </remarks>
#if CSHARPAUTHOR_PUBLIC_API
public
#endif
class LabeledJumpStatement : BaseOutputComponent
{
    private readonly LabeledJumpKind _kind;
    private readonly string _label;

    /// <summary>Jumps out of, or on in, the loop labelled <paramref name="label"/>.</summary>
    public LabeledJumpStatement(LabeledJumpKind kind, string label)
    {
        _kind = kind;
        _label = label ?? throw new ArgumentNullException(nameof(label));
    }

    /// <summary>The loop this jump names.</summary>
    public string Label => _label;

    /// <summary>Break or continue.</summary>
    public LabeledJumpKind Kind => _kind;

    /// <summary>Where the jump is. Kept for callers that set it; nothing reads it now.</summary>
    /// <remarks>
    /// It named the jump in the capability diagnostic this used to raise. There is no capability
    /// question left to ask, so there is no diagnostic to name it in.
    /// </remarks>
    public string? Context { get; set; }

    /// <summary>
    /// The label a downlevelled jump targets: <c>outer_break</c>, <c>outer_continue</c>.
    /// </summary>
    public static string SyntheticLabel(string label, LabeledJumpKind kind) =>
        label + (kind == LabeledJumpKind.Break ? "_break" : "_continue");

    /// <inheritdoc />
    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        // Unconditional. This used to ask the profile whether it could write `break outer;` and
        // the profile said yes at C# 15 and above, and under any plain OutputContext with no
        // profile driving it at all - which is the path every facade example takes. C# has no
        // labeled break or continue at any version, so that output was CS1002, CS0103 and CS0201
        // in the consumer's build. The goto is not the downlevel form; it is the only form.
        var target = SyntheticLabel(_label, _kind);

        outputContext.EmitSession().MarkLabelUsed(target);

        outputContext.WriteIndentedLine("goto " + target + ";");
    }
}
