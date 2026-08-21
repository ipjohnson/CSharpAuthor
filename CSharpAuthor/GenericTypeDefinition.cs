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

    /// <summary>
    /// A closed generic with array specifiers and an annotation for each level, outermost first,
    /// then one for the element - <c>[1]</c> with <c>[false, true]</c> is <c>Name&lt;T&gt;?[]</c>.
    /// </summary>
    /// <remarks>
    /// Every parameter is required, so this cannot be reached by a call that means the
    /// <c>bool isNullable</c> overload above.
    /// </remarks>
    public GenericTypeDefinition(TypeDefinitionEnum classType, string ns, string name, IReadOnlyList<ITypeDefinition> closingTypes,
        IReadOnlyList<int>? arrayRanks, IReadOnlyList<bool>? nullableAnnotations, ITypeDefinition? containingType)
        : base(classType, ns, name, arrayRanks, nullableAnnotations, containingType)
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

        WriteArraySuffix(stringBuilder);

        return stringBuilder.ToString();
    }

    /// <summary>
    /// The arguments are part of the value, and they are compared where every other part of it is -
    /// in the name the type writes. Walking <c>_closingTypes</c> here could only ever answer for a
    /// second <see cref="GenericTypeDefinition"/>, so a closed generic arriving from anywhere else
    /// was reported different from an identical one built by hand.
    /// </summary>
    public override int CompareTo(ITypeDefinition other)
    {
        return TypeDefinitionIdentity.KeyCompare(TypeKey, other);
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
            WriteArraySuffix(builder, ArrayRanks, NullableAnnotations);

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

        WriteArraySuffix(builder);
    }

    public override ITypeDefinition MakeNullable(bool nullable = true)
    {
        return new GenericTypeDefinition(TypeDefinitionEnum, Namespace, Name, _closingTypes, ArrayRanks, AnnotationsWithOuterAnnotation(nullable), ContainingType);
    }

    /// <inheritdoc cref="TypeDefinition.MakeArray(int)" />
    public override ITypeDefinition MakeArray(int rank)
    {
        return new GenericTypeDefinition(TypeDefinitionEnum, Namespace, Name, _closingTypes, ArrayRanksWithOuterRank(rank), AnnotationsWithOuterLevel(), ContainingType);
    }

    public ITypeDefinition MakeOpenType()
    {
        var emptyTypes = _closingTypes.Select(_ => TypeDefinition.Get("", "")).ToArray();

        return new GenericTypeDefinition(TypeDefinitionEnum, Namespace, Name, emptyTypes, ArrayRanks, NullableAnnotations, ContainingType);
    }
        
    public override IReadOnlyList<ITypeDefinition> TypeArguments => _closingTypes;
}