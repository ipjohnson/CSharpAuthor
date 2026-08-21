using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor.Roslyn;

/// <summary>One element of a tuple type: a type, and the name it was given if it was given one.</summary>
public sealed class TupleElementDefinition
{
    public TupleElementDefinition(ITypeDefinition type, string? name = null)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Name = name;
    }

    public ITypeDefinition Type { get; }

    /// <summary>The element name, or null for <c>Item1</c>-style positional elements.</summary>
    public string? Name { get; }
}

/// <summary>
/// A tuple type, written in tuple syntax — <c>(int a, string b)</c>, not
/// <c>ValueTuple&lt;int, string&gt;</c>.
/// </summary>
/// <remarks>
/// The element names are part of the type as written and are not recoverable from the underlying
/// <c>ValueTuple</c>, so they are carried here. They are also the reason a tuple is not just a
/// generic: <c>ValueTuple&lt;int, string&gt;</c> is the same runtime type as <c>(int a, string b)</c>
/// and reads nothing like it at a call site. No namespace is contributed — tuple syntax needs no
/// <c>using</c>, whatever the element types need is contributed by the elements themselves.
/// </remarks>
public sealed class TupleTypeDefinition : ITypeDefinition
{
    private readonly IReadOnlyList<TupleElementDefinition> _elements;
    private readonly ITypeDefinition[] _typeArguments;
    private int? _hashCode;

    public TupleTypeDefinition(IReadOnlyList<TupleElementDefinition> elements, bool isNullable = false)
    {
        if (elements == null)
        {
            throw new ArgumentNullException(nameof(elements));
        }

        if (elements.Count < 2)
        {
            throw new ArgumentException("A tuple type has at least two elements", nameof(elements));
        }

        _elements = elements;
        IsNullable = isNullable;

        var typeArguments = new ITypeDefinition[elements.Count];

        for (var i = 0; i < elements.Count; i++)
        {
            typeArguments[i] = elements[i].Type;
        }

        _typeArguments = typeArguments;
    }

    public IReadOnlyList<TupleElementDefinition> Elements => _elements;

    public TypeDefinitionEnum TypeDefinitionEnum => TypeDefinitionEnum.ClassDefinition;

    public bool IsNullable { get; }

    public bool IsArray => false;

    /// <summary>The underlying type's name, which is what a symbol reports for a tuple.</summary>
    public string Name => "ValueTuple";

    public string Namespace => "System";

    /// <summary>
    /// The elements' namespaces only. Tuple syntax is built in, so the tuple itself never needs an
    /// import for <c>System</c>.
    /// </summary>
    public IEnumerable<string> KnownNamespaces
    {
        get
        {
            foreach (var element in _elements)
            {
                foreach (var knownNamespace in element.Type.KnownNamespaces)
                {
                    yield return knownNamespace;
                }
            }
        }
    }

    public IReadOnlyList<ITypeDefinition> TypeArguments => _typeArguments;

    public void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName)
    {
        builder.Append('(');

        for (var i = 0; i < _elements.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            _elements[i].Type.WriteTypeName(builder, typeOutputMode);

            var name = _elements[i].Name;

            if (!string.IsNullOrEmpty(name))
            {
                builder.Append(' ');
                builder.Append(name);
            }
        }

        builder.Append(')');

        if (IsNullable)
        {
            builder.Append('?');
        }
    }

    public ITypeDefinition MakeNullable(bool nullable = true)
    {
        return new TupleTypeDefinition(_elements, nullable);
    }

    public ITypeDefinition MakeArray()
    {
        return new ArrayTypeDefinition(this);
    }

    /// <summary>
    /// The rank of each array wrapping this type, outermost first. Empty: this type is not an array.
    /// </summary>
    /// <remarks>
    /// Present so the bridge's types satisfy the type model's array-rank contract without a change
    /// at merge time.
    /// </remarks>
    public IReadOnlyList<int> ArrayRanks => Array.Empty<int>();

    /// <summary>The type this one is declared inside. A tuple is declared inside nothing, which is what a tuple symbol reports.</summary>
    public ITypeDefinition? ContainingType => null;

    /// <summary>An array of this type with the given rank.</summary>
    public ITypeDefinition MakeArray(int rank)
    {
        return new ArrayTypeDefinition(this, rank);
    }

    public int CompareTo(ITypeDefinition? other)
    {
        if (ReferenceEquals(other, null))
        {
            return 1;
        }

        if (other is not TupleTypeDefinition tuple)
        {
            var otherCompare = string.Compare(ToString(), other.ToString(), StringComparison.Ordinal);

            return otherCompare != 0 ? otherCompare : -1;
        }

        return string.Compare(ToString(), tuple.ToString(), StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is TupleTypeDefinition tuple && CompareTo(tuple) == 0;
    }

    public override int GetHashCode()
    {
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        return _hashCode ??= ToString().GetHashCode();
    }

    public override string ToString()
    {
        var builder = new StringBuilder();

        WriteTypeName(builder, TypeOutputMode.FullName);

        return builder.ToString();
    }
}
