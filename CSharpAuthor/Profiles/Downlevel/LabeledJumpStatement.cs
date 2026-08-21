using System;

namespace CSharpAuthor;

/// <summary>Which loop keyword a labeled jump is.</summary>
public enum LabeledJumpKind
{
    /// <summary>Leaves the labeled loop.</summary>
    Break,

    /// <summary>Starts the labeled loop's next iteration.</summary>
    Continue
}

/// <summary>
/// <c>break outer;</c> or <c>continue outer;</c>, and the <c>goto</c> that has meant the same
/// thing since C# 1.
/// </summary>
/// <remarks>
/// The label a downlevelled jump targets is declared by the
/// <see cref="LabeledLoopStatement"/> it names, which only declares the ones something actually
/// jumps to - so this records the use rather than assuming it.
/// </remarks>
public class LabeledJumpStatement : BaseOutputComponent
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

    /// <summary>Where the jump is, named in any diagnostic it produces.</summary>
    public string? Context { get; set; }

    /// <summary>
    /// The label a downlevelled jump targets: <c>outer_break</c>, <c>outer_continue</c>.
    /// </summary>
    public static string SyntheticLabel(string label, LabeledJumpKind kind) =>
        label + (kind == LabeledJumpKind.Break ? "_break" : "_continue");

    /// <inheritdoc />
    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        var session = outputContext.EmitSession();

        if (session.MayEmit(LanguageFeature.LabeledJumps, outputContext, Context))
        {
            outputContext.WriteIndentedLine(
                (_kind == LabeledJumpKind.Break ? "break " : "continue ") + _label + ";");

            return;
        }

        var target = SyntheticLabel(_label, _kind);

        session.MarkLabelUsed(target);

        outputContext.WriteIndentedLine("goto " + target + ";");
    }
}
