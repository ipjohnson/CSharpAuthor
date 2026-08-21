using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// A <c>switch</c> statement and its arms.
/// </summary>
/// <remarks>
/// Arms are written in the order they were added, with <c>default:</c> last wherever it was asked
/// for. Nothing adds a <c>break</c>: each arm needs one of its own, or a
/// <see cref="BaseBlockDefinition.Return"/>, because C# does not let a non-empty arm fall through.
/// </remarks>
public class SwitchBlockDefinition : BaseOutputComponent
{
    private readonly IOutputComponent _switchValue;
    private readonly List<CaseBlockDefinition> _cases = new ();
    private CaseBlockDefinition? _default;

    /// <summary>
    /// A switch over <paramref name="switchValue"/>. Prefer
    /// <see cref="BaseBlockDefinition.Switch"/>, which builds one and attaches it to a block.
    /// </summary>
    public SwitchBlockDefinition(IOutputComponent switchValue)
    {
        _switchValue = switchValue;
    }

    /// <summary>
    /// The <c>default:</c> arm, written after every case whichever order it was asked for in.
    /// </summary>
    /// <remarks>
    /// Reused rather than appended: a second call returns the same block, so statements from both
    /// calls land in one arm. That is deliberate - a switch has at most one default, and two would
    /// not compile.
    /// </remarks>
    public CaseBlockDefinition AddDefault()
    {
        return _default ??= new CaseBlockDefinition (CodeOutputComponent.Get("default:"));
    }

    /// <summary>
    /// A <c>case</c> arm. Statements go on the block this returns.
    /// </summary>
    /// <remarks>
    /// <paramref name="value"/> is a value rather than text, so it is written as the C# literal
    /// that denotes it: a <see cref="string"/> is quoted, and an <c>enum</c> is written
    /// <c>Type.Member</c> with the type left unrendered so the file derives its namespace. That
    /// last one is the reason not to hand-write the arm - <c>ToString()</c> on an enum gives the
    /// bare member name, which is CS0103.
    /// </remarks>
    public CaseBlockDefinition AddCase(object value)
    {
        var caseBlock = new CaseBlockDefinition(WrapCaseStatement(value));

        _cases.Add(caseBlock);

        return caseBlock;
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.WriteIndent("switch (");
        _switchValue.WriteOutput(outputContext);
        outputContext.WriteLine(")");
        outputContext.OpenScope();
        foreach (var caseBlockDefinition in _cases)
        {
            caseBlockDefinition.WriteOutput(outputContext);
        }
        _default?.WriteOutput(outputContext);
        outputContext.CloseScope();
    }

    private WrapStatement WrapCaseStatement(object value)
    {
        var outputComponent = CodeOutputComponent.Get(value);

        return new WrapStatement(outputComponent, "case ", ":");
    }
}