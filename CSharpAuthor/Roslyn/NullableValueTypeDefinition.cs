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
    /// <remarks>
    /// The base forwards the no-argument form to this one, so only the rank overload is overridden.
    /// </remarks>
    public override ITypeDefinition MakeArray(int rank)
    {
        return new ArrayTypeDefinition(this, rank);
    }

    // CompareTo, Equals and GetHashCode are the base's. It compares the name a type writes, and this
    // one writes `int?` exactly as a hand-built TypeDefinition.Get(typeof(int)).MakeNullable() does,
    // so the two match in both directions without anything here. The override that used to sit here
    // could not: it fell back to comparing ToString(), which on this type is the base's 1.x form -
    // namespace and name, with the arguments and the `?` missing - so two different closings of one
    // nullable generic compared equal to each other and a nullable generic compared unequal to its
    // hand-built self.
}
