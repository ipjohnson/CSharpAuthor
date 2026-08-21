using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class ForEachDefinition : BaseBlockDefinition
{
    private readonly IOutputComponent _enumerableStatement;
    private readonly ITypeDefinition? _variableType;

    public ForEachDefinition(string instanceName, IOutputComponent enumerableStatement)
        : this(null, instanceName, enumerableStatement)
    {
    }

    /// <summary>
    /// A loop that declares its variable with an explicit type rather than <c>var</c>.
    /// </summary>
    /// <remarks>
    /// The keyword used to be part of the literal <c>"foreach(var "</c>, which made every loop an
    /// inferred one - so the cast that <c>foreach (Widget w in objects)</c> performs had nowhere to
    /// be expressed. Passing the type also lets it reach import derivation, which a variable
    /// declared as <c>var</c> never needed to.
    /// </remarks>
    public ForEachDefinition(
        ITypeDefinition? variableType, string instanceName, IOutputComponent enumerableStatement)
    {
        _variableType = variableType;
        _enumerableStatement = enumerableStatement;
        Instance = new InstanceDefinition(instanceName);
    }

    public InstanceDefinition Instance { get; }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.WriteIndent("foreach (");

        if (_variableType == null)
        {
            outputContext.Write(KeyWords.Var);
        }
        else
        {
            outputContext.Write(_variableType);
        }

        outputContext.WriteSpace();

        Instance.WriteOutput(outputContext);
        outputContext.Write(" in ");
        _enumerableStatement.WriteOutput(outputContext);
        outputContext.WriteLine(")");

        WriteBlock(outputContext);
    }
}
