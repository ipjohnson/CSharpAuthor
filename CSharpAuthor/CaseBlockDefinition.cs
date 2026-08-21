using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// One arm of a <see cref="SwitchBlockDefinition"/> - a <c>case</c> label or <c>default:</c>, and
/// the statements under it.
/// </summary>
/// <remarks>
/// An arm is indented under its label but is not braced, which is how C# writes a switch section.
/// It needs its own <see cref="BaseBlockDefinition.Break"/> or
/// <see cref="BaseBlockDefinition.Return"/>; nothing adds one.
/// </remarks>
public class CaseBlockDefinition : BaseBlockDefinition
{
    private readonly IOutputComponent _caseStatement;

    /// <summary>
    /// An arm labelled by <paramref name="caseStatement"/>. Built by
    /// <see cref="SwitchBlockDefinition.AddCase"/> and
    /// <see cref="SwitchBlockDefinition.AddDefault"/>; the label carries its own <c>case</c> and
    /// <c>:</c>, so constructing one directly means supplying both.
    /// </summary>
    public CaseBlockDefinition(IOutputComponent caseStatement)
    {
        _caseStatement = caseStatement;
    }
        
    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.WriteIndent();
        _caseStatement.WriteOutput(outputContext);
        outputContext.WriteLine();

        outputContext.IncrementIndent();

        foreach (var caseBlockDefinition in StatementList)
        {
            caseBlockDefinition.WriteOutput(outputContext);
        }

        outputContext.DecrementIndent();
    }
}