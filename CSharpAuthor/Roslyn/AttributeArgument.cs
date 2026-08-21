using System;
using System.Collections.Generic;

namespace CSharpAuthor.Roslyn;

/// <summary>What an attribute argument turned out to be.</summary>
public enum AttributeArgumentKind
{
    /// <summary>A <c>null</c> literal, or a default the compiler could not resolve.</summary>
    Null,

    /// <summary>A number, a bool or a char.</summary>
    Primitive,

    /// <summary>A string.</summary>
    String,

    /// <summary>A <c>typeof(...)</c>. The value is an <c>ITypeDefinition</c>.</summary>
    Type,

    /// <summary>An enum member, or a combination of them. The value is the underlying constant.</summary>
    Enum,

    /// <summary>An array, including the trailing <c>params</c> arguments. The value is the elements.</summary>
    Array,

    /// <summary>An argument Roslyn could not bind.</summary>
    Unknown
}

/// <summary>
/// One argument of an attribute, read from metadata and kept as a value rather than as text.
/// </summary>
/// <remarks>
/// <para>
/// The reason this is not a string is the reason the type model exists. An attribute argument copied
/// out of the consumer's source carries their <c>using</c> directives with it, and the generated file
/// has none — so <c>[CacheControl(Type = CacheControlEnum.NoStore)]</c> re-emitted verbatim is CS0103
/// in the generated output, and the workaround is to write every enum fully qualified at every call
/// site. Held as a type and a member name, the same argument qualifies itself at serialization,
/// under whatever output mode the file ends up using.
/// </para>
/// <para>
/// Folding the enum to its constant is the other failure: an enum's constant value is an integer, so
/// <c>MaxAge | Public</c> emits <c>33</c>, which needs a cast to assign back and reads as nothing at
/// all. The member names are recovered here, including the combination case, and a value that names
/// no members falls back to a cast rather than to a bare number.
/// </para>
/// </remarks>
public sealed class AttributeArgument
{
    public AttributeArgument(
        AttributeArgumentKind kind,
        object? value,
        ITypeDefinition? type = null,
        string? name = null,
        IReadOnlyList<string>? enumMemberNames = null,
        ITypeDefinition? arrayElementType = null)
    {
        Kind = kind;
        Value = value;
        Type = type;
        Name = name;
        EnumMemberNames = enumMemberNames ?? Array.Empty<string>();
        ArrayElementType = arrayElementType;
    }

    /// <summary>
    /// The property name for a named argument, the parameter name for a positional one, or null when
    /// the parameter could not be resolved.
    /// </summary>
    public string? Name { get; }

    /// <summary>The argument's declared type.</summary>
    public ITypeDefinition? Type { get; }

    public AttributeArgumentKind Kind { get; }

    /// <summary>
    /// The value: a boxed primitive, a string, an <c>ITypeDefinition</c> for <c>typeof</c>, the
    /// underlying constant for an enum, or an <c>IReadOnlyList&lt;AttributeArgument&gt;</c> for an
    /// array.
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// The enum members the value names — one for a plain member, several for a combination of
    /// flags, none when the value names nothing the enum declares.
    /// </summary>
    public IReadOnlyList<string> EnumMemberNames { get; }

    /// <summary>
    /// The element type of an array argument. Kept alongside the array type because the model's
    /// flattened array shape cannot be un-arrayed again, and <c>new T[] { ... }</c> needs the
    /// element type, not the array's.
    /// </summary>
    public ITypeDefinition? ArrayElementType { get; }

    /// <summary>The elements of an array argument.</summary>
    public IReadOnlyList<AttributeArgument> Elements =>
        Value as IReadOnlyList<AttributeArgument> ?? Array.Empty<AttributeArgument>();

    /// <summary>
    /// The argument as something a writer can emit, with every type reference still unrendered.
    /// </summary>
    public IOutputComponent GetOutputComponent()
    {
        switch (Kind)
        {
            case AttributeArgumentKind.Null:
                return CodeOutputComponent.Get("null");

            case AttributeArgumentKind.String:
                return CodeOutputComponent.Get(SyntaxHelpers.QuoteString((string)Value!));

            case AttributeArgumentKind.Type:
                return Value is ITypeDefinition typeValue
                    ? SyntaxHelpers.TypeOf(typeValue)
                    : CodeOutputComponent.Get("null");

            case AttributeArgumentKind.Enum:
                return EnumComponent();

            case AttributeArgumentKind.Array:
                return ArrayComponent();

            default:
                return CodeOutputComponent.Get(Value);
        }
    }

    private IOutputComponent EnumComponent()
    {
        if (Type == null)
        {
            return CodeOutputComponent.Get(Value);
        }

        if (EnumMemberNames.Count == 1)
        {
            return new StaticPropertyStatement(Type, EnumMemberNames[0]) { Indented = false };
        }

        if (EnumMemberNames.Count > 1)
        {
            var members = new List<IOutputComponent>(EnumMemberNames.Count);

            foreach (var memberName in EnumMemberNames)
            {
                members.Add(new StaticPropertyStatement(Type, memberName) { Indented = false });
            }

            return new LogicStatement(" | ", members) { PrintParentheses = false, Indented = false };
        }

        // A value that names no member is still that value, and a bare integer does not assign to an
        // enum-typed property.
        return new StaticCastComponent(Type, CodeOutputComponent.Get(Value));
    }

    private IOutputComponent ArrayComponent()
    {
        var elements = Elements;

        var components = new IOutputComponent[elements.Count];

        for (var i = 0; i < elements.Count; i++)
        {
            components[i] = elements[i].GetOutputComponent();
        }

        var elementType = ArrayElementType
                          ?? (Type is ArrayTypeDefinition arrayType ? arrayType.ElementType : null)
                          ?? TypeDefinition.Get("", "object");

        return new NewArrayStatement(elementType, components) { Indented = false };
    }

    public override string ToString()
    {
        return Name == null ? Kind + ": " + Value : Name + " = " + Value;
    }
}
