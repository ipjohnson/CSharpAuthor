using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace CSharpAuthor.Roslyn;

/// <summary>
/// Reads attributes off symbols into values the writers can emit.
/// </summary>
/// <remarks>
/// Works from <c>AttributeData</c> — the bound form — rather than from <c>AttributeSyntax</c>. The
/// syntax is what both consumers read today, and it costs them: the arguments arrive as source text
/// that has to be re-qualified with a syntax rewriter to survive being copied into a file with no
/// <c>using</c> directives, an attribute on another part of a partial is invisible, and an argument
/// referring to a <c>const</c> is copied as the constant's name rather than its value.
/// </remarks>
public static class SymbolAttributeExtensions
{
    /// <summary>Every attribute on the symbol.</summary>
    public static IReadOnlyList<AttributeInstance> GetAttributeInstances(this ISymbol symbol)
    {
        if (symbol == null)
        {
            throw new ArgumentNullException(nameof(symbol));
        }

        var attributes = symbol.GetAttributes();

        var instances = new List<AttributeInstance>(attributes.Length);

        foreach (var attribute in attributes)
        {
            instances.Add(attribute.GetAttributeInstance());
        }

        return instances;
    }

    /// <summary>
    /// The first attribute of the named type, matched with or without the <c>Attribute</c> suffix.
    /// </summary>
    public static AttributeInstance? FindAttribute(this ISymbol symbol, ITypeDefinition attributeType)
    {
        if (symbol == null)
        {
            throw new ArgumentNullException(nameof(symbol));
        }

        foreach (var attribute in symbol.GetAttributes())
        {
            var instance = attribute.GetAttributeInstance();

            if (instance.Is(attributeType))
            {
                return instance;
            }
        }

        return null;
    }

    public static bool HasAttribute(this ISymbol symbol, ITypeDefinition attributeType)
    {
        return symbol.FindAttribute(attributeType) != null;
    }

    /// <summary>One bound attribute, as a value.</summary>
    public static AttributeInstance GetAttributeInstance(this AttributeData attributeData)
    {
        if (attributeData == null)
        {
            throw new ArgumentNullException(nameof(attributeData));
        }

        var attributeClass = attributeData.AttributeClass;

        var attributeType = attributeClass == null
            ? TypeDefinition.Get("", "Attribute")
            : attributeClass.GetTypeDefinition();

        var parameters = attributeData.AttributeConstructor?.Parameters ?? default;

        var constructorArguments = new List<AttributeArgument>(attributeData.ConstructorArguments.Length);

        for (var i = 0; i < attributeData.ConstructorArguments.Length; i++)
        {
            var parameterName = !parameters.IsDefaultOrEmpty && i < parameters.Length ? parameters[i].Name : null;

            constructorArguments.Add(ConvertArgument(attributeData.ConstructorArguments[i], parameterName));
        }

        var namedArguments = new List<AttributeArgument>(attributeData.NamedArguments.Length);

        foreach (var namedArgument in attributeData.NamedArguments)
        {
            namedArguments.Add(ConvertArgument(namedArgument.Value, namedArgument.Key));
        }

        return new AttributeInstance(attributeType, constructorArguments, namedArguments);
    }

    private static AttributeArgument ConvertArgument(TypedConstant constant, string? name)
    {
        var type = constant.Type == null ? null : constant.Type.GetTypeDefinition();

        switch (constant.Kind)
        {
            case TypedConstantKind.Array:
                return ConvertArrayArgument(constant, type, name);

            case TypedConstantKind.Type:
                return new AttributeArgument(
                    AttributeArgumentKind.Type,
                    constant.Value is ITypeSymbol typeSymbol ? typeSymbol.GetTypeDefinition() : null,
                    type,
                    name);

            case TypedConstantKind.Enum:
                return new AttributeArgument(
                    AttributeArgumentKind.Enum,
                    constant.Value,
                    type,
                    name,
                    EnumMemberNames(constant));

            case TypedConstantKind.Primitive:
                if (constant.Value == null)
                {
                    return new AttributeArgument(AttributeArgumentKind.Null, null, type, name);
                }

                return new AttributeArgument(
                    constant.Value is string ? AttributeArgumentKind.String : AttributeArgumentKind.Primitive,
                    constant.Value,
                    type,
                    name);

            default:
                return new AttributeArgument(
                    constant.IsNull ? AttributeArgumentKind.Null : AttributeArgumentKind.Unknown,
                    constant.IsNull ? null : constant.Value,
                    type,
                    name);
        }
    }

    private static AttributeArgument ConvertArrayArgument(TypedConstant constant, ITypeDefinition? type, string? name)
    {
        if (constant.IsNull)
        {
            return new AttributeArgument(AttributeArgumentKind.Null, null, type, name);
        }

        var values = constant.Values;

        var elements = new List<AttributeArgument>(values.Length);

        foreach (var value in values)
        {
            elements.Add(ConvertArgument(value, null));
        }

        var elementType = constant.Type is IArrayTypeSymbol arrayType
            ? arrayType.ElementType.GetTypeDefinition()
            : null;

        return new AttributeArgument(
            AttributeArgumentKind.Array,
            elements,
            type,
            name,
            null,
            elementType);
    }

    /// <summary>
    /// The members an enum constant names.
    /// </summary>
    /// <remarks>
    /// One name for a plain member. For a combination, the members whose bits the value is made of,
    /// in declaration order — a flags value is what the attribute was written as, and the integer it
    /// folds to is not assignable back to the property. Nothing at all when the value names no
    /// member, which is a legal enum value and has to be emitted as a cast.
    /// </remarks>
    private static IReadOnlyList<string> EnumMemberNames(TypedConstant constant)
    {
        if (constant.Type is not INamedTypeSymbol enumType)
        {
            return Array.Empty<string>();
        }

        var target = ToInt64(constant.Value);

        if (target == null)
        {
            return Array.Empty<string>();
        }

        var members = new List<KeyValuePair<string, long>>();

        foreach (var member in enumType.GetMembers())
        {
            if (member is not IFieldSymbol { HasConstantValue: true } field)
            {
                continue;
            }

            var value = ToInt64(field.ConstantValue);

            if (value == null)
            {
                continue;
            }

            if (value.Value == target.Value)
            {
                return new[] { RoslynSyntaxFacts.Escape(field.Name) };
            }

            members.Add(new KeyValuePair<string, long>(RoslynSyntaxFacts.Escape(field.Name), value.Value));
        }

        var remaining = target.Value;

        var names = new List<string>();

        // Widest first, so a member that is itself a combination is preferred over the bits it is
        // made of — which is how the value was most likely written.
        members.Sort((left, right) => BitCount(right.Value).CompareTo(BitCount(left.Value)));

        foreach (var member in members)
        {
            if (member.Value != 0 && (remaining & member.Value) == member.Value)
            {
                names.Add(member.Key);
                remaining &= ~member.Value;
            }
        }

        return remaining == 0 && names.Count > 0 ? names : Array.Empty<string>();
    }

    private static int BitCount(long value)
    {
        var count = 0;

        var bits = unchecked((ulong)value);

        while (bits != 0)
        {
            count++;
            bits &= bits - 1;
        }

        return count;
    }

    /// <summary>
    /// The constant as a long, without going through a culture-aware conversion.
    /// </summary>
    private static long? ToInt64(object? value)
    {
        switch (value)
        {
            case sbyte sbyteValue: return sbyteValue;
            case byte byteValue: return byteValue;
            case short shortValue: return shortValue;
            case ushort ushortValue: return ushortValue;
            case int intValue: return intValue;
            case uint uintValue: return uintValue;
            case long longValue: return longValue;
            case ulong ulongValue: return unchecked((long)ulongValue);
            case char charValue: return charValue;
            case bool boolValue: return boolValue ? 1 : 0;
            default: return null;
        }
    }
}
