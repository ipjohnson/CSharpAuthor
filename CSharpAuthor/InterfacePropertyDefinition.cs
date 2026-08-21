using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// A property declared on an interface: <see cref="PropertyDefinition"/> with the accessibility
/// keyword left off.
/// </summary>
/// <remarks>
/// The interface decides accessibility, so <see cref="BaseOutputComponent.Modifiers"/> is ignored
/// here. Everything else reads the same, including that a property named <c>this</c> with an index
/// declares an indexer.
/// </remarks>
public class InterfacePropertyDefinition : PropertyDefinition
{
    /// <summary>
    /// A property named <paramref name="name"/>. Prefer
    /// <see cref="InterfaceDefinition.AddProperty(ITypeDefinition, string)"/>, which builds one and
    /// attaches it.
    /// </summary>
    public InterfacePropertyDefinition(ITypeDefinition typeDefinition, string name) : base(typeDefinition, name)
    {

    }

    protected override void WriteAccessModifiers(IOutputContext outputContext)
    {
        outputContext.WriteIndent();
    }
}