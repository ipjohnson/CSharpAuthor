using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor.Roslyn;

/// <summary>
/// One link in a nested type's name: an identifier and the type arguments that close it.
/// </summary>
public sealed class NestedTypeSegment
{
    public NestedTypeSegment(string name, IReadOnlyList<ITypeDefinition>? typeArguments = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeArguments = typeArguments ?? Array.Empty<ITypeDefinition>();
    }

    public string Name { get; }

    public IReadOnlyList<ITypeDefinition> TypeArguments { get; }
}

/// <summary>
/// A nested type whose containers carry type arguments of their own —
/// <c>Outer&lt;int&gt;.Inner&lt;string&gt;.Deepest</c>.
/// </summary>
/// <remarks>
/// <para>
/// A nested type whose containers are not generic is a plain <c>TypeDefinition</c> with a dotted
/// name, which is what both consumers build today and what this bridge still returns for that case.
/// It stops working the moment a container is closed over something: <c>GenericTypeDefinition</c>
/// holds one name and one argument list, so <c>Outer&lt;int&gt;.Inner&lt;string&gt;</c> has nowhere
/// to put the <c>int</c>. Folding it into the name — <c>"Outer&lt;int&gt;.Inner"</c> — would render
/// correctly and break the thing the type model exists for, because a name is text and text cannot
/// be re-qualified at serialization time. Each segment therefore keeps its arguments as unrendered
/// type definitions.
/// </para>
/// <para>
/// A container closed over its own type parameters is the ordinary case inside a declaration:
/// <c>Outer&lt;T&gt;.Inner&lt;U&gt;</c> arrives with <c>T</c> and <c>U</c> as type parameter
/// symbols and renders as itself.
/// </para>
/// </remarks>
public sealed class NestedTypeDefinition : ITypeDefinition
{
    private readonly IReadOnlyList<NestedTypeSegment> _segments;
    private int? _hashCode;
    private string? _key;

    public NestedTypeDefinition(
        TypeDefinitionEnum typeDefinitionEnum,
        string ns,
        IReadOnlyList<NestedTypeSegment> segments,
        bool isNullable = false)
    {
        if (segments == null)
        {
            throw new ArgumentNullException(nameof(segments));
        }

        if (segments.Count == 0)
        {
            throw new ArgumentException("A nested type has at least one segment", nameof(segments));
        }

        TypeDefinitionEnum = typeDefinitionEnum;
        Namespace = ns;
        _segments = segments;
        IsNullable = isNullable;
    }

    public IReadOnlyList<NestedTypeSegment> Segments => _segments;

    public TypeDefinitionEnum TypeDefinitionEnum { get; }

    public bool IsNullable { get; }

    public bool IsArray => false;

    /// <summary>
    /// The dotted identifier path without type arguments — <c>Outer.Inner.Deepest</c> — which is
    /// what a caller reading <c>Name</c> off a nested type has always been given.
    /// </summary>
    public string Name
    {
        get
        {
            var builder = new StringBuilder();

            for (var i = 0; i < _segments.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('.');
                }

                builder.Append(_segments[i].Name);
            }

            return builder.ToString();
        }
    }

    public string Namespace { get; }

    public IEnumerable<string> KnownNamespaces
    {
        get
        {
            foreach (var segment in _segments)
            {
                foreach (var typeArgument in segment.TypeArguments)
                {
                    foreach (var knownNamespace in typeArgument.KnownNamespaces)
                    {
                        yield return knownNamespace;
                    }
                }
            }

            yield return Namespace;
        }
    }

    /// <summary>The arguments of the type itself, not of its containers.</summary>
    public IReadOnlyList<ITypeDefinition> TypeArguments => _segments[_segments.Count - 1].TypeArguments;

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

        for (var i = 0; i < _segments.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('.');
            }

            var segment = _segments[i];

            builder.Append(segment.Name);

            if (segment.TypeArguments.Count == 0)
            {
                continue;
            }

            builder.Append('<');

            for (var argument = 0; argument < segment.TypeArguments.Count; argument++)
            {
                if (argument > 0)
                {
                    builder.Append(',');
                }

                segment.TypeArguments[argument].WriteTypeName(builder, typeOutputMode);
            }

            builder.Append('>');
        }

        if (IsNullable)
        {
            builder.Append('?');
        }
    }

    public ITypeDefinition MakeNullable(bool nullable = true)
    {
        return new NestedTypeDefinition(TypeDefinitionEnum, Namespace, _segments, nullable);
    }

    public ITypeDefinition MakeArray()
    {
        return new ArrayTypeDefinition(this);
    }

    /// <summary>An array of this type with the given rank.</summary>
    public ITypeDefinition MakeArray(int rank)
    {
        return new ArrayTypeDefinition(this, rank);
    }

    /// <summary>This type is not an array.</summary>
    public IReadOnlyList<int> ArrayRanks => Array.Empty<int>();

    /// <summary>
    /// Where each <c>?</c> sits: one flag per array level, then one for the element. This type is
    /// not an array, so it is the single flag <see cref="IsNullable"/>.
    /// </summary>
    /// <remarks>
    /// Present so the bridge's types satisfy the type model's nullable-position contract, the same
    /// way <see cref="ArrayRanks"/> answers its array-rank contract.
    /// </remarks>
    public IReadOnlyList<bool> NullableAnnotations => BaseTypeDefinition.OuterAnnotationOnly(1, IsNullable);

    /// <summary>
    /// The container, with its own arguments — the <c>Outer&lt;int&gt;</c> of
    /// <c>Outer&lt;int&gt;.Inner&lt;string&gt;</c>, and null when there is only one segment.
    /// </summary>
    public ITypeDefinition? ContainingType
    {
        get
        {
            if (_segments.Count < 2)
            {
                return null;
            }

            var containerSegments = new NestedTypeSegment[_segments.Count - 1];

            for (var i = 0; i < containerSegments.Length; i++)
            {
                containerSegments[i] = _segments[i];
            }

            return new NestedTypeDefinition(TypeDefinitionEnum, Namespace, containerSegments);
        }
    }

    /// <inheritdoc cref="BaseTypeDefinition.TypeKey" />
    private string TypeKey => _key ??= TypeDefinitionIdentity.KeyOf(this);

    public int CompareTo(ITypeDefinition? other)
    {
        return TypeDefinitionIdentity.KeyCompare(TypeKey, other);
    }

    public override bool Equals(object? obj)
    {
        return TypeDefinitionIdentity.KeyEquals(TypeKey, obj);
    }

    public override int GetHashCode()
    {
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        return _hashCode ??= TypeKey.GetHashCode();
    }

    public override string ToString()
    {
        var builder = new StringBuilder();

        WriteTypeName(builder, TypeOutputMode.FullName);

        return builder.ToString();
    }
}
