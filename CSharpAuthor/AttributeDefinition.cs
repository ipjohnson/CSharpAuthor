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

    public string? ArgumentStatement { get; set; }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        var argumentListString = GetArgumentListString();
        var attributeName = _attributeType.Name;

        if (attributeName.EndsWith(AttributePostfix))
        {
            attributeName = attributeName.Substring(0, attributeName.Length - AttributePostfix.Length);
        }

        outputContext.AddImportNamespace(_attributeType);

        outputContext.WriteIndentedLine($"[{attributeName}{argumentListString}]");
    }

    private string GetArgumentListString()
    {
        if (string.IsNullOrEmpty(ArgumentStatement))
        {
            return "";
        }

        return $"({ArgumentStatement})";
    }
}