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

    /// <summary>An attribute reference is never itself an array, so it carries no ranks.</summary>
    public IReadOnlyList<int> ArrayRanks => _attributeType.ArrayRanks;

    /// <summary>
    /// The declared name, <em>with</em> the postfix. An alias has to name the type that exists.
    /// </summary>
    public string Name => _attributeType.Name;

    public string Namespace => _attributeType.Namespace;

    /// <summary>Delegated, so a nested attribute keeps its container.</summary>
    public ITypeDefinition? ContainingType => _attributeType.ContainingType;

    public IEnumerable<string> KnownNamespaces => _attributeType.KnownNamespaces;

    public IReadOnlyList<ITypeDefinition> TypeArguments => _attributeType.TypeArguments;

    /// <summary>The name as it is written in brackets, with the postfix taken off.</summary>
    public string WrittenName
    {
        get
        {
            var builder = new StringBuilder();

            WriteTypeName(builder);

            return builder.ToString();
        }
    }

    /// <summary>
    /// Writes the type exactly as it writes itself, and then takes the postfix off its simple name.
    /// </summary>
    /// <remarks>
    /// Delegating rather than rebuilding the name out of <c>Namespace</c> and <c>Name</c> is the
    /// point: whatever the type model knows how to write - a containing type, generic arguments,
    /// a qualification the mode asked for - is written here too. Rebuilding it drops exactly the
    /// parts a type knows about itself and a name does not, which is how a nested attribute lost
    /// its container and a generic one lost its arguments.
    /// </remarks>
    public void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName)
    {
        var start = builder.Length;

        _attributeType.WriteTypeName(builder, typeOutputMode);

        // The simple name ends where the type argument list begins, or at the end.
        var end = builder.Length;

        for (var i = start; i < builder.Length; i++)
        {
            if (builder[i] == '<')
            {
                end = i;
                break;
            }
        }

        if (end - start <= AttributePostfix.Length)
        {
            return;
        }

        for (var i = 0; i < AttributePostfix.Length; i++)
        {
            if (builder[end - AttributePostfix.Length + i] != AttributePostfix[i])
            {
                return;
            }
        }

        builder.Remove(end - AttributePostfix.Length, AttributePostfix.Length);
    }

    public ITypeDefinition MakeNullable(bool nullable = true) => _attributeType.MakeNullable(nullable);

    public ITypeDefinition MakeArray() => _attributeType.MakeArray();

    public ITypeDefinition MakeArray(int rank) => _attributeType.MakeArray(rank);

    /// <summary>
    /// The shared ordering. Answering -1 to everything that was not another attribute reference
    /// made this smaller than every type and, read the other way round, larger than none of them -
    /// the <see cref="IComparable{T}"/> contract broken the same way the type definitions had it
    /// broken.
    /// </summary>
    public int CompareTo(ITypeDefinition other)
    {
        return TypeDefinitionOrder.Compare(this, other);
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
