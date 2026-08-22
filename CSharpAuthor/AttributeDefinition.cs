using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// One attribute on a declaration: <c>[Obsolete("use Greeter2")]</c>.
/// </summary>
/// <remarks>
/// Built by <see cref="BaseOutputComponent.AddAttribute(ITypeDefinition, object[])"/>, which is
/// where the arguments are given. What is reached on the object it returns is
/// <see cref="Target"/> - the <c>[property: Key]</c> form a positional record needs.
/// </remarks>
public class AttributeDefinition : BaseOutputComponent
{
    private readonly ITypeDefinition _attributeType;
    private readonly AttributeTypeReference? _writtenType;

    /// <summary>
    /// An attribute of <paramref name="attributeType"/>. Prefer
    /// <see cref="BaseOutputComponent.AddAttribute(ITypeDefinition, object[])"/>, which builds one
    /// and attaches it.
    /// </summary>
    /// <remarks>
    /// The type is wrapped in an <see cref="AttributeTypeReference"/>, which is what takes the
    /// <c>Attribute</c> postfix off when it is written while keeping the declared name for the
    /// namespace and for any alias.
    /// </remarks>
    public AttributeDefinition(ITypeDefinition attributeType)
    {
        _attributeType = attributeType;
        _writtenType = attributeType == null ? null : new AttributeTypeReference(attributeType);
    }

    /// <summary>
    /// The arguments, written in order inside <c>( )</c>. Null or empty writes no parentheses at
    /// all - <c>[Flags]</c>, not <c>[Flags()]</c>.
    /// </summary>
    /// <remarks>
    /// Components, so a string literal needs <see cref="SyntaxHelpers.QuoteString"/> and a type
    /// needs <see cref="SyntaxHelpers.TypeOf"/>. For a named argument use
    /// <see cref="AddNamedArgument"/>, which keeps it after the positional ones - C# requires that
    /// order, and a named argument added to this list would sit wherever it was added.
    /// </remarks>
    public IList<IOutputComponent>? Arguments { get; set; }

    /// <summary>
    /// The named arguments, written after the positional ones as <c>Name = value</c>.
    /// </summary>
    /// <remarks>
    /// Held separately from <see cref="Arguments"/> rather than pre-formatted into it, because C#
    /// requires every positional argument to precede every named one - and because a name is an
    /// identifier, so it is escaped rather than written through.
    /// </remarks>
    public IList<KeyValuePair<string, IOutputComponent>>? NamedArguments { get; set; }

    /// <summary>
    /// Adds <c><paramref name="name"/> = <paramref name="value"/></c> to the attribute.
    /// </summary>
    /// <example>
    /// <code>
    /// var attribute = new AttributeDefinition(TypeDefinition.Get(typeof(ObsoleteAttribute)));
    ///
    /// attribute.AddNamedArgument("IsError", SyntaxHelpers.True);
    /// // [Obsolete(IsError = true)]
    /// </code>
    /// </example>
    public AttributeDefinition AddNamedArgument(string name, IOutputComponent value)
    {
        NamedArguments ??= new List<KeyValuePair<string, IOutputComponent>>();

        NamedArguments.Add(new KeyValuePair<string, IOutputComponent>(name, value));

        return this;
    }

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
        // Same reason as ParameterDefinition.WriteWithSignature: this bypasses
        // BaseOutputComponent.WriteOutput, which is the only other place UsingNamespaces is read.
        if (UsingNamespaces != null)
        {
            outputContext.AddImportNamespaces(UsingNamespaces);
        }

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
        outputContext.Write(_writtenType!);

        WriteArguments(outputContext);
    }

    private void WriteArguments(IOutputContext outputContext)
    {
        var positional = Arguments is { Count: > 0 };
        var named = NamedArguments is { Count: > 0 };

        if (!positional && !named)
        {
            // No parentheses at all - `[Flags]`, not `[Flags()]`.
            return;
        }

        outputContext.Write("(");

        if (positional)
        {
            Arguments!.OutputCommaSeparatedList(outputContext);
        }

        if (named)
        {
            var first = !positional;

            foreach (var argument in NamedArguments!)
            {
                if (!first)
                {
                    outputContext.Write(", ");
                }

                outputContext.Write(CSharpIdentifier.Escape(argument.Key));
                outputContext.Write(" = ");
                argument.Value.WriteOutput(outputContext);

                first = false;
            }
        }

        outputContext.Write(")");
    }

}