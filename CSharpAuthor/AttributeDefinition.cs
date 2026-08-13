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

    /// <summary>
    /// The declaration the attribute applies to, written as <c>[target: Attr]</c>.
    /// </summary>
    /// <remarks>
    /// Needed wherever one syntactic position declares more than one thing and the attribute would
    /// otherwise land on the wrong one. A positional record is the case that forces it: an attribute
    /// on the parameter stays on the parameter, so a constraint meant for the property is silently
    /// never seen. <c>"property"</c> is the target that fixes it; <c>"return"</c>, <c>"field"</c> and
    /// <c>"assembly"</c> behave the same way.
    /// </remarks>
    public string? Target { get; set; }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.AddImportNamespace(_attributeType);

        outputContext.WriteIndent("[");
        WriteBody(outputContext);
        outputContext.WriteLine("]");
    }

    /// <summary>
    /// Writes the attribute with no indent and no trailing newline, for a position that is part of
    /// a line rather than a line of its own - a parameter, say.
    /// </summary>
    public void WriteInline(IOutputContext outputContext)
    {
        outputContext.AddImportNamespace(_attributeType);

        outputContext.Write("[");
        WriteBody(outputContext);
        outputContext.Write("] ");
    }

    private void WriteBody(IOutputContext outputContext)
    {
        var attributeName = _attributeType.Name;

        if (attributeName.EndsWith(AttributePostfix))
        {
            attributeName = attributeName.Substring(0, attributeName.Length - AttributePostfix.Length);
        }

        if (!string.IsNullOrEmpty(Target))
        {
            outputContext.Write(Target!);
            outputContext.Write(": ");
        }

        outputContext.Write(attributeName);
        WriteArguments(outputContext);
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