using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class AttributeDefinition : BaseOutputComponent
{
    private const string AttributePostfix = "Attribute";
    private readonly ITypeDefinition _attributeType;

    public AttributeDefinition(ITypeDefinition attributeType)
    {
        _attributeType = attributeType;
    }

    public IList<IOutputComponent>? Arguments { get; set; }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        var attributeName = _attributeType.Name;

        if (attributeName.EndsWith(AttributePostfix))
        {
            attributeName = attributeName.Substring(0, attributeName.Length - AttributePostfix.Length);
        }

        outputContext.AddImportNamespace(_attributeType);

        outputContext.WriteIndent($"[{attributeName}");
        WriteArguments(outputContext);
        outputContext.WriteLine("]");
    }

    private void WriteArguments(IOutputContext outputContext)
    {
        if (Arguments is { Count: > 0 })
        {
            outputContext.Write("(");
            Arguments.OutputCommaSeparatedList(outputContext);
            outputContext.Write(")");
        }
    }

}