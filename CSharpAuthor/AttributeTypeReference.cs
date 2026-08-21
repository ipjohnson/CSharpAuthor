using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// An attribute type as it is written in an attribute list: the same type, without the
/// <c>Attribute</c> the language lets you leave off.
/// </summary>
/// <remarks>
/// It exists so an attribute goes through <see cref="IOutputContext.Write(ITypeDefinition)"/> like
/// everything else instead of being written as a bare string with its namespace declared on the
/// side. Written as a string it carried no namespace, so the file needed a <c>using</c> that the
/// writer had to remember to ask for - and asking for it in a mode that qualifies every name is
/// what put a stray directive at the top of files that should have had none.
///
/// Being a type rather than a string, it is also qualified when the mode qualifies, and it takes
/// part in the name plan: two attributes of the same name from different namespaces are told apart
/// the same way two ordinary types are.
/// </remarks>
public sealed class AttributeTypeReference : ITypeDefinition
{
    private const string AttributePostfix = "Attribute";

    private readonly ITypeDefinition _attributeType;

    public AttributeTypeReference(ITypeDefinition attributeType)
    {
        _attributeType = attributeType ?? throw new ArgumentNullException(nameof(attributeType));
    }

    /// <summary>The type this stands for, named as it is declared.</summary>
    public ITypeDefinition AttributeType => _attributeType;

    public TypeDefinitionEnum TypeDefinitionEnum => _attributeType.TypeDefinitionEnum;

    public bool IsNullable => false;

    public bool IsArray => false;

    /// <summary>
    /// The declared name, <em>with</em> the postfix. An alias has to name the type that exists.
    /// </summary>
    public string Name => _attributeType.Name;

    public string Namespace => _attributeType.Namespace;

    public IEnumerable<string> KnownNamespaces => _attributeType.KnownNamespaces;

    public IReadOnlyList<ITypeDefinition> TypeArguments => _attributeType.TypeArguments;

    /// <summary>The name as it is written in brackets, with the postfix taken off.</summary>
    public string WrittenName
    {
        get
        {
            var name = _attributeType.Name;

            return name.Length > AttributePostfix.Length && name.EndsWith(AttributePostfix, StringComparison.Ordinal)
                ? name.Substring(0, name.Length - AttributePostfix.Length)
                : name;
        }
    }

    public void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName)
    {
        if (!string.IsNullOrEmpty(Namespace))
        {
            if (typeOutputMode == TypeOutputMode.Global)
            {
                builder.Append("global::");
                builder.Append(Namespace);
                builder.Append('.');
            }
            else if (typeOutputMode == TypeOutputMode.FullName)
            {
                builder.Append(Namespace);
                builder.Append('.');
            }
        }

        builder.Append(WrittenName);

        var typeArguments = TypeArguments;

        if (typeArguments == null || typeArguments.Count == 0)
        {
            return;
        }

        builder.Append('<');

        for (var i = 0; i < typeArguments.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            typeArguments[i].WriteTypeName(builder, typeOutputMode);
        }

        builder.Append('>');
    }

    public ITypeDefinition MakeNullable(bool nullable = true) => _attributeType.MakeNullable(nullable);

    public ITypeDefinition MakeArray() => _attributeType.MakeArray();

    public int CompareTo(ITypeDefinition other)
    {
        if (other is not AttributeTypeReference attributeType)
        {
            return -1;
        }

        return _attributeType.CompareTo(attributeType._attributeType);
    }

    public override bool Equals(object obj)
    {
        return obj is AttributeTypeReference other && _attributeType.Equals(other._attributeType);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return _attributeType.GetHashCode() * 31 + 17;
        }
    }

    public override string ToString() => _attributeType.ToString();
}
