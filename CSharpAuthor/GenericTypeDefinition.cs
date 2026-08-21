using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpAuthor;

public class GenericTypeDefinition : BaseTypeDefinition
{
    private readonly IReadOnlyList<ITypeDefinition> _closingTypes;

    public GenericTypeDefinition(Type type, IReadOnlyList<ITypeDefinition> closeTypes, bool isArray = false,
        bool isNullable = false) :
        this(type.IsInterface ? TypeDefinitionEnum.InterfaceDefinition : TypeDefinitionEnum.ClassDefinition, type.Namespace!, type.GetGenericName(),  closeTypes, isArray, isNullable)
    {

    }

    public GenericTypeDefinition(TypeDefinitionEnum classType, string ns, string name, IReadOnlyList<ITypeDefinition> closingTypes,
        bool isArray = false, bool isNullable = false) : base(classType, ns, name, isArray, isNullable)
    {
        _closingTypes = closingTypes;
    }

    public GenericTypeDefinition(TypeDefinitionEnum classType, string ns, string name, IReadOnlyList<ITypeDefinition> closingTypes,
        IReadOnlyList<int>? arrayRanks, bool isNullable = false, ITypeDefinition? containingType = null)
        : base(classType, ns, name, arrayRanks, isNullable, containingType)
    {
        _closingTypes = closingTypes;
    }

    internal GenericTypeDefinition(TypeDefinitionEnum classType, string ns, string name, IReadOnlyList<ITypeDefinition> closingTypes,
        IReadOnlyList<int>? arrayRanks, bool isNullable, bool isElementNullable, ITypeDefinition? containingType)
        : base(classType, ns, name, arrayRanks, isNullable, isElementNullable, containingType)
    {
        _closingTypes = closingTypes;
    }

    /// <inheritdoc cref="TypeDefinition.ToString" />
    public override string ToString()
    {
        var stringBuilder = new StringBuilder();

        stringBuilder.Append(Namespace);
        stringBuilder.Append('.');
        stringBuilder.Append(Name);
        stringBuilder.Append('<');

        var comma = false;

        foreach (var closingType in _closingTypes)
        {
            if (comma)
            {
                stringBuilder.Append(',');
            }
            else
            {
                comma = true;
            }

            stringBuilder.Append(closingType);
        }

        stringBuilder.Append('>');

        WriteArrayRanks(stringBuilder);

        if (IsNullable)
        {
            stringBuilder.Append('?');
        }

        return stringBuilder.ToString();
    }

    /// <summary>
    /// The shared ordering, type arguments included - which is what the two sides used to disagree
    /// about, because the plain definition does not know they exist.
    /// </summary>
    public override int CompareTo(ITypeDefinition other)
    {
        return BaseCompareTo(other);
    }

    public override IEnumerable<string> KnownNamespaces
    {
        get
        {
            foreach (var typeDefinition in _closingTypes)
            {
                foreach (var knownNamespace in typeDefinition.KnownNamespaces)
                {
                    yield return knownNamespace;
                }
            }

            if (ContainingType != null)
            {
                foreach (var knownNamespace in ContainingType.KnownNamespaces)
                {
                    yield return knownNamespace;
                }
            }

            yield return Namespace;
        }
    }

    public override void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName)
    {
        WriteQualifier(builder, typeOutputMode);

        builder.Append(WrittenName());

        // An empty argument list is `Thing<>`, which is only legal inside typeof - CS1031 in a
        // field, a parameter or a base type. A generic definition closed over nothing names the
        // type it was built from, which is the only reading that is a type at all.
        if (_closingTypes.Count == 0)
        {
            WriteArrayRanks(builder);

            if (IsNullable)
            {
                builder.Append('?');
            }

            return;
        }

        builder.Append('<');

        var writeComma = false;

        foreach (var typeDefinition in _closingTypes)
        {
            if (writeComma)
            {
                builder.Append(',');
            }
            else
            {
                writeComma = true;
            }

            typeDefinition.WriteTypeName(builder, typeOutputMode);
        }

        builder.Append('>');

        WriteArrayRanks(builder);

        if (IsNullable)
        {
            builder.Append("?");
        }
    }

    public override ITypeDefinition MakeNullable(bool nullable = true)
    {
        return new GenericTypeDefinition(
            TypeDefinitionEnum, Namespace, Name, _closingTypes, ArrayRanks, nullable, IsElementNullable, ContainingType);
    }

    /// <inheritdoc cref="TypeDefinition.MakeArray(int)" />
    public override ITypeDefinition MakeArray(int rank)
    {
        return new GenericTypeDefinition(
            TypeDefinitionEnum,
            Namespace,
            Name,
            _closingTypes,
            ArrayRanksWithOuterRank(rank),
            IsNullable,
            IsElementNullable,
            ContainingType);
    }

    public ITypeDefinition MakeOpenType()
    {
        var emptyTypes = _closingTypes.Select(_ => TypeDefinition.Get("", "")).ToArray();

        return new GenericTypeDefinition(TypeDefinitionEnum, Namespace, Name, emptyTypes, ArrayRanks, IsNullable, ContainingType);
    }
        
    public override IReadOnlyList<ITypeDefinition> TypeArguments => _closingTypes;
}