using System;
using System.Collections.Generic;

namespace CSharpAuthor.Roslyn;

/// <summary>
/// One attribute as it was written on a declaration: its type, its positional arguments and its
/// property assignments.
/// </summary>
/// <remarks>
/// Read from the symbol rather than from the syntax. An attribute on a partial declared in another
/// file, one inherited through metadata, or one whose arguments came from a <c>const</c> is present
/// in the symbol and absent from the syntax node in front of the generator.
/// </remarks>
public sealed class AttributeInstance
{
    private const string AttributeSuffix = "Attribute";

    public AttributeInstance(
        ITypeDefinition attributeType,
        IReadOnlyList<AttributeArgument> constructorArguments,
        IReadOnlyList<AttributeArgument> namedArguments)
    {
        AttributeType = attributeType ?? throw new ArgumentNullException(nameof(attributeType));
        ConstructorArguments = constructorArguments ?? throw new ArgumentNullException(nameof(constructorArguments));
        NamedArguments = namedArguments ?? throw new ArgumentNullException(nameof(namedArguments));
    }

    /// <summary>The attribute class, under its declared name — <c>SingletonAttribute</c>.</summary>
    public ITypeDefinition AttributeType { get; }

    /// <summary>The positional arguments, in order, named after the parameters they bound to.</summary>
    public IReadOnlyList<AttributeArgument> ConstructorArguments { get; }

    /// <summary>The <c>Name = value</c> assignments.</summary>
    public IReadOnlyList<AttributeArgument> NamedArguments { get; }

    /// <summary>A property assignment by name.</summary>
    public AttributeArgument? FindNamedArgument(string name)
    {
        foreach (var argument in NamedArguments)
        {
            if (string.Equals(argument.Name, name, StringComparison.Ordinal))
            {
                return argument;
            }
        }

        return null;
    }

    /// <summary>A positional argument by the name of the parameter it bound to.</summary>
    public AttributeArgument? FindConstructorArgument(string parameterName)
    {
        foreach (var argument in ConstructorArguments)
        {
            if (string.Equals(argument.Name, parameterName, StringComparison.Ordinal))
            {
                return argument;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether this is the attribute the type definition names, with or without the
    /// <c>Attribute</c> suffix.
    /// </summary>
    public bool Is(ITypeDefinition attributeType)
    {
        if (attributeType == null)
        {
            return false;
        }

        if (!string.Equals(AttributeType.Namespace, attributeType.Namespace, StringComparison.Ordinal))
        {
            return false;
        }

        var wanted = attributeType.Name;

        if (string.Equals(AttributeType.Name, wanted, StringComparison.Ordinal))
        {
            return true;
        }

        return !wanted.EndsWith(AttributeSuffix, StringComparison.Ordinal) &&
               string.Equals(AttributeType.Name, wanted + AttributeSuffix, StringComparison.Ordinal);
    }

    /// <summary>
    /// The arguments as a writer takes them: the positional ones in order, then the property
    /// assignments as <c>Name = value</c>. Every type reference is still unrendered.
    /// </summary>
    public IList<IOutputComponent> GetArgumentComponents()
    {
        var components = new List<IOutputComponent>(ConstructorArguments.Count + NamedArguments.Count);

        foreach (var argument in ConstructorArguments)
        {
            components.Add(argument.GetOutputComponent());
        }

        foreach (var argument in NamedArguments)
        {
            components.Add(new WrapStatement(
                CodeOutputComponent.Get(" = "),
                CodeOutputComponent.Get(argument.Name),
                argument.GetOutputComponent()));
        }

        return components;
    }

    /// <summary>
    /// The attribute as a writeable declaration, arguments and all — what re-emitting an attribute
    /// the consumer wrote onto generated code takes.
    /// </summary>
    public AttributeDefinition ToAttributeDefinition(string? target = null)
    {
        var definition = new AttributeDefinition(AttributeType) { Target = target };

        var arguments = GetArgumentComponents();

        if (arguments.Count > 0)
        {
            definition.Arguments = arguments;
        }

        return definition;
    }

    public override string ToString()
    {
        return AttributeType.Name;
    }
}
