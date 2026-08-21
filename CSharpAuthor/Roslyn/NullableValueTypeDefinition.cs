using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor.Roslyn;

/// <summary>
/// <c>Nullable&lt;T&gt;</c> — the <c>int?</c> kind of nullable, as opposed to the <c>string?</c>
/// kind.
/// </summary>
/// <remarks>
/// <para>
/// Both render a trailing <c>?</c> and the type model has one bit for it, so the two arrive at the
/// same <c>ITypeDefinition</c> and stop being distinguishable. They are not the same thing.
/// <c>int?</c> is a different runtime type from <c>int</c>; <c>string?</c> is <c>string</c> with an
/// annotation the runtime never sees. <c>typeof(int?)</c> compiles and <c>typeof(string?)</c> is
/// CS8639. An emitter targeting a language version without nullable reference types must drop the
/// <c>?</c> from one and keep it on the other, and it can only do that if the conversion recorded
/// which one it had.
/// </para>
/// <para>
/// It derives from <c>TypeDefinition</c> so that a bridged <c>int?</c> still compares equal to a
/// hand-built <c>TypeDefinition.Get(typeof(int)).MakeNullable()</c> in both directions — that
/// comparison is how a generator matches a parameter against a registration, and a bridge that
/// quietly broke it would not be adoptable. <c>ToString</c> and <c>GetHashCode</c> are deliberately
/// left alone for the same reason: they are the base's hash contract, and equal objects have to
/// hash alike.
/// </para>
/// </remarks>
public sealed class NullableValueTypeDefinition : TypeDefinition
{
    private readonly ITypeDefinition[] _typeArguments;

    public NullableValueTypeDefinition(ITypeDefinition underlyingType)
        : base(
            underlyingType == null
                ? throw new ArgumentNullException(nameof(underlyingType))
                : underlyingType.TypeDefinitionEnum,
            underlyingType.Namespace,
            underlyingType.Name,
            underlyingType.IsArray,
            true)
    {
        UnderlyingType = underlyingType;
        _typeArguments = new[] { underlyingType };
    }

    /// <summary>The <c>T</c> of <c>Nullable&lt;T&gt;</c>.</summary>
    public ITypeDefinition UnderlyingType { get; }

    public override IEnumerable<string> KnownNamespaces => UnderlyingType.KnownNamespaces;

    public override IReadOnlyList<ITypeDefinition> TypeArguments => _typeArguments;

    public override void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName)
    {
        UnderlyingType.WriteTypeName(builder, typeOutputMode);

        builder.Append('?');
    }

    /// <summary>
    /// Removing the nullability of a <c>Nullable&lt;T&gt;</c> yields <c>T</c>, not a value type with
    /// a bit cleared.
    /// </summary>
    public override ITypeDefinition MakeNullable(bool nullable = true)
    {
        return nullable ? this : UnderlyingType;
    }

    /// <summary>
    /// <c>int?[]</c>, not <c>int[]?</c> — the array is of the nullable type, so the annotation stays
    /// on the element.
    /// </summary>
    public override ITypeDefinition MakeArray()
    {
        return new ArrayTypeDefinition(this);
    }

    public override int CompareTo(ITypeDefinition other)
    {
        if (ReferenceEquals(other, null))
        {
            return 1;
        }

        var baseCompare = base.CompareTo(other);

        if (baseCompare != 0)
        {
            return baseCompare;
        }

        if (other is NullableValueTypeDefinition nullableValue)
        {
            return UnderlyingType.CompareTo(nullableValue.UnderlyingType);
        }

        // The base compared name, namespace and nullability, which settles it for a type that is not
        // generic — that is the case where a hand-built nullable has to keep matching. Two closings
        // of the same generic get that far and are not the same type, so they are separated by the
        // arguments the base never saw.
        if (other.TypeArguments.Count == 0 && UnderlyingType.TypeArguments.Count == 0)
        {
            return 0;
        }

        return string.Compare(ToString(), other.ToString(), StringComparison.Ordinal);
    }
}
