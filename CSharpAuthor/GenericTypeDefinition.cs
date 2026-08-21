using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpAuthor;

public class GenericTypeDefinition : BaseTypeDefinition
{
    private int? _hashCode;
    private readonly IReadOnlyList<ITypeDefinition> _closingTypes;
    private readonly bool _isOpenType;

    public GenericTypeDefinition(Type type, IReadOnlyList<ITypeDefinition> closeTypes, bool isArray = false,
        bool isNullable = false) :
        this(type.IsInterface ? TypeDefinitionEnum.InterfaceDefinition : TypeDefinitionEnum.ClassDefinition, type.Namespace!, type.GetGenericName(),  closeTypes, isArray, isNullable)
    {

    }

    public GenericTypeDefinition(TypeDefinitionEnum classType, string ns, string name, IReadOnlyList<ITypeDefinition> closingTypes,
        bool isArray = false, bool isNullable = false, ITypeDefinition? containingType = null)
        : base(classType, ns, name, isArray, isNullable, containingType)
    {
        _closingTypes = closingTypes;
    }

    private GenericTypeDefinition(TypeDefinitionEnum classType, string ns, string name, IReadOnlyList<ITypeDefinition> closingTypes,
        bool isArray, bool isNullable, ITypeDefinition? containingType, bool isOpenType)
        : this(classType, ns, name, closingTypes, isArray, isNullable, containingType)
    {
        _isOpenType = isOpenType;
    }

    public override bool Equals(object? obj)
    {
        if (obj is GenericTypeDefinition genericTypeDefinition)
        {
            return CompareTo(genericTypeDefinition) == 0;
        }

        return false;
    }

    public override int GetHashCode()
    {
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        return _hashCode ??= ToString().GetHashCode(); 
    }

    public override string ToString()
    {
        var stringBuilder = new StringBuilder();

        stringBuilder.Append(Namespace);
        stringBuilder.Append('.');
        stringBuilder.Append(Name);

        WriteTypeArguments(stringBuilder, (b, closingType) => b.Append(closingType));

        if (IsArray)
        {
            stringBuilder.Append("[]");
        }

        if (IsNullable)
        {
            stringBuilder.Append('?');
        }

        return stringBuilder.ToString();
    }

    public override int CompareTo(ITypeDefinition? other)
    {
        var baseCompare = BaseCompareTo(other);

        if (baseCompare != 0)
        {
            return baseCompare;
        }

        if (other is not GenericTypeDefinition genericTypeDefinition)
        {
            return -1;
        }

        if (genericTypeDefinition._closingTypes.Count != _closingTypes.Count)
        {
            return genericTypeDefinition._closingTypes.Count - _closingTypes.Count;
        }

        for (var i = 0; i < _closingTypes.Count; i++)
        {
            var compareValue = _closingTypes[i].CompareTo(genericTypeDefinition._closingTypes[i]);

            if (compareValue != 0)
            {
                return compareValue;
            }
        }

        return 0;
    }

    public override IEnumerable<string> KnownNamespaces
    {
        get
        {
            // An open type writes no arguments, so importing their namespaces would add usings
            // for names that never appear in the file.
            if (!_isOpenType)
            {
                foreach (var typeDefinition in _closingTypes)
                {
                    foreach (var knownNamespace in typeDefinition.KnownNamespaces)
                    {
                        yield return knownNamespace;
                    }
                }
            }

            yield return Namespace;
        }
    }
    
    public override void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName)
    {
        WriteQualification(builder, typeOutputMode);

        builder.Append(Name);

        WriteTypeArguments(builder, (b, typeDefinition) => typeDefinition.WriteTypeName(b, typeOutputMode));

        if (IsArray)
        {
            builder.Append("[]");
        }

        if (IsNullable)
        {
            builder.Append("?");
        }   
    }

    public override ITypeDefinition MakeNullable(bool nullable = true)
    {
        return new GenericTypeDefinition(TypeDefinitionEnum, Namespace, Name, _closingTypes, IsArray, nullable, ContainingType, _isOpenType);
    }

    /// <inheritdoc cref="ArrayTypeDefinition.MakeArray"/>
    public override ITypeDefinition MakeArray()
    {
        return new ArrayTypeDefinition(this);
    }

    /// <summary>
    /// The unbound form of this type - <c>Dictionary&lt;,&gt;</c> for a
    /// <c>Dictionary&lt;string, int&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Openness used to be faked by swapping the type arguments for empty ones, which rendered as
    /// <c>Dictionary&lt;.,.&gt;</c> - each blank argument still wrote the <c>.</c> joining its
    /// namespace to its name. Recording it instead keeps the arity available and leaves the
    /// arguments intact for anything that asks.
    /// </remarks>
    public ITypeDefinition MakeOpenType()
    {
        return new GenericTypeDefinition(
            TypeDefinitionEnum, Namespace, Name, _closingTypes, IsArray, IsNullable, ContainingType, isOpenType: true);
    }

    /// <summary>
    /// Writes <c>&lt;...&gt;</c>, either the argument list or the commas alone when the type is
    /// open.
    /// </summary>
    private void WriteTypeArguments(StringBuilder builder, Action<StringBuilder, ITypeDefinition> writeArgument)
    {
        // A generic with no arguments is not generic, and <> is not a type. Writing nothing is
        // what an empty argument list means.
        if (_closingTypes.Count == 0)
        {
            return;
        }

        builder.Append('<');

        if (_isOpenType)
        {
            builder.Append(new string(',', _closingTypes.Count - 1));
        }
        else
        {
            var writeSeparator = false;

            foreach (var typeDefinition in _closingTypes)
            {
                if (writeSeparator)
                {
                    builder.Append(", ");
                }
                else
                {
                    writeSeparator = true;
                }

                writeArgument(builder, typeDefinition);
            }
        }

        builder.Append('>');
    }
        
    public override IReadOnlyList<ITypeDefinition> TypeArguments => _closingTypes;
}