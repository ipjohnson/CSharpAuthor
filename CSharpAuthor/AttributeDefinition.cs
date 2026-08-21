using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class AttributeDefinition : BaseOutputComponent
{
    private readonly ITypeDefinition _attributeType;
    private readonly AttributeTypeReference _writtenType;

    public AttributeDefinition(ITypeDefinition attributeType)
    {
        _attributeType = attributeType;
        _writtenType = new AttributeTypeReference(attributeType);
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
        outputContext.Write("[");
        WriteBody(outputContext);
        outputContext.Write("] ");
    }

    private void WriteBody(IOutputContext outputContext)
    {
        if (!string.IsNullOrEmpty(Target))
        {
            outputContext.Write(Target!);
            outputContext.Write(": ");
        }

        // Written as a type rather than as its name, so the namespace it needs is derived from the
        // file rather than declared beside it - and so it is qualified when the mode qualifies.
        outputContext.Write(_writtenType);

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