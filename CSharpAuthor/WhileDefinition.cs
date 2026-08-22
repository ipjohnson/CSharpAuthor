using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// A <c>while</c> loop. Built by <see cref="BaseBlockDefinition.While"/>.
/// </summary>
public class WhileDefinition : BaseBlockDefinition
{
    private readonly IOutputComponent _testStatement;

    /// <summary>
    /// A loop testing <paramref name="testStatement"/>. Prefer
    /// <see cref="BaseBlockDefinition.While"/>, which builds one and attaches it to a block.
    /// </summary>
    public WhileDefinition(object testStatement)
    {
        _testStatement = CodeOutputComponent.Get(testStatement);
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.WriteIndent("while (");
        _testStatement.WriteOutput(outputContext);
        outputContext.WriteLine(")");

        WriteBlock(outputContext);
    }
}
