using System;

namespace CSharpAuthor.Profiles;

/// <summary>
/// A loop an outer-loop jump can name, plus the <c>goto</c> labels those jumps target.
/// </summary>
/// <remarks>
/// <para>
/// The loop owns its body because the downlevel needs somewhere to put the label a
/// <c>continue outer;</c> jumps to - the end of this loop's body - and the label a
/// <c>break outer;</c> jumps to, just past it.
/// </para>
/// <para>
/// Only the labels something actually jumped to are declared. Declaring both every time would
/// trade a language feature for a pair of CS0164 warnings on every loop, which is a downlevel
/// that compiles and that nobody would have written by hand.
/// </para>
/// </remarks>
#if CSHARPAUTHOR_PUBLIC_API
public
#endif
class LabeledLoopStatement : BaseBlockDefinition
{
    private readonly string _label;
    private readonly IOutputComponent _header;

    /// <summary>
    /// A labelled loop.
    /// </summary>
    /// <param name="label">The name a jump uses.</param>
    /// <param name="header">
    /// The loop's header, without its body: <c>foreach (var row in grid)</c>. Written at the
    /// current indent, so a component passed here should not indent itself.
    /// </param>
    public LabeledLoopStatement(string label, IOutputComponent header)
    {
        _label = label ?? throw new ArgumentNullException(nameof(label));
        _header = header ?? throw new ArgumentNullException(nameof(header));
    }

    /// <inheritdoc cref="LabeledLoopStatement(string,IOutputComponent)" />
    public LabeledLoopStatement(string label, string header)
        : this(label, new CodeOutputComponent(header) { Indented = false })
    {
    }

    /// <summary>The name a jump uses.</summary>
    public string Label => _label;

    /// <summary>Where the loop is, named in any diagnostic it produces.</summary>
    public string? Context { get; set; }

    /// <inheritdoc />
    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        var session = outputContext.EmitSession();

        // No `outer:` declaration. Nothing jumps to it: LabeledJumpStatement always targets the
        // synthetic labels below, because C# has no labeled break or continue to target this one.
        outputContext.WriteIndent();
        _header.WriteOutput(outputContext);
        outputContext.WriteLine();

        outputContext.OpenScope();

        foreach (var statement in StatementList)
        {
            statement.WriteOutput(outputContext);
        }

        WriteSyntheticLabel(outputContext, session, LabeledJumpKind.Continue);

        outputContext.CloseScope();

        WriteSyntheticLabel(outputContext, session, LabeledJumpKind.Break);
    }

    private void WriteSyntheticLabel(IOutputContext outputContext, EmitSession session, LabeledJumpKind kind)
    {
        var target = LabeledJumpStatement.SyntheticLabel(_label, kind);

        if (!session.WasLabelUsed(target))
        {
            return;
        }

        // A label has to precede a statement, and there is nothing left to do here.
        outputContext.WriteIndentedLine(target + ": ;");

        session.ClearLabel(target);
    }
}
