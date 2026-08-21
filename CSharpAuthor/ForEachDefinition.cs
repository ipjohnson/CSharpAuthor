using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// A <c>foreach</c> loop. Built by <see cref="BaseBlockDefinition.ForEach"/>.
/// </summary>
/// <remarks>
/// The loop variable is always declared <c>var</c>; there is no form here that names its type.
/// </remarks>
public class ForEachDefinition : BaseBlockDefinition
{
    private readonly IOutputComponent _enumerableStatement;

    /// <summary>
    /// A loop binding each element to <paramref name="instanceName"/>. Prefer
    /// <see cref="BaseBlockDefinition.ForEach"/>, which builds one and attaches it to a block.
    /// </summary>
    public ForEachDefinition(string instanceName, IOutputComponent enumerableStatement)
    {
        _enumerableStatement = enumerableStatement;
        Instance = new InstanceDefinition(instanceName);
    }

    /// <summary>
    /// The loop variable as a value, for building statements in the body.
    /// </summary>
    /// <remarks>
    /// Reading it back is what keeps the body and the declaration in step - the alternative is
    /// repeating the name as a string in both places and hoping they stay equal.
    /// </remarks>
    public InstanceDefinition Instance { get; }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.WriteIndent("foreach(var ");
        Instance.WriteOutput(outputContext);
        outputContext.Write(" in ");
        _enumerableStatement.WriteOutput(outputContext);
        outputContext.WriteLine(")");
            
        WriteBlock(outputContext);
    }
}