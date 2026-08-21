using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace CSharpAuthor.Roslyn;

/// <summary>
/// Converts Roslyn symbols into the type model, which is the first thing every source generator
/// written against this library needs.
/// </summary>
/// <remarks>
/// <para>
/// There was no such conversion, so every generator wrote one - and every one of them got the same
/// cases wrong, because the cases are not obvious. A nested type's <c>Name</c> is <c>Inner</c>, so
/// writing it alone binds to whatever <c>Inner</c> is in scope at the point of use. <c>int?</c>
/// arrives as <c>Nullable&lt;int&gt;</c>. <c>int[][]</c> is an array of arrays, not an array with a
/// flag. And the keyword table that turns <c>Int32</c> into <c>int</c> was private and keyed on
/// <see cref="Type"/>, which a generator does not have.
/// </para>
/// <para>
/// Ships as source in the same package rather than as a second one, gated on
/// <c>PackageCSharpAuthorIncludeRoslyn</c>. A generator project already references Roslyn, so this
/// compiles into it with no new dependency.
/// </para>
/// </remarks>
public static class SymbolExtensions
{
    /// <summary>
    /// The type model equivalent of a Roslyn type symbol, rendered lazily like any other - so it
    /// still picks up short names, <c>global::</c> qualification and import derivation from the
    /// output context.
    /// </summary>
    public static ITypeDefinition GetTypeDefinition(this ITypeSymbol typeSymbol)
    {
        if (typeSymbol == null)
        {
            throw new ArgumentNullException(nameof(typeSymbol));
        }

        var typeDefinition = GetUnannotatedTypeDefinition(typeSymbol);

        // A nullable value type is Nullable<T> and is handled with the generics; this is the
        // reference-type annotation, which carries no wrapper to notice. An unconstrained type
        // parameter is neither a value type nor a reference type and takes the annotation too.
        if (typeSymbol.NullableAnnotation == NullableAnnotation.Annotated && !typeSymbol.IsValueType)
        {
            return typeDefinition.MakeNullable();
        }

        return typeDefinition;
    }

    private static ITypeDefinition GetUnannotatedTypeDefinition(ITypeSymbol typeSymbol)
    {
        switch (typeSymbol)
        {
            // Element plus rank, recursively - int[][] is an array of int[], and int[,] is one
            // array of rank 2. A single bool could express neither.
            case IArrayTypeSymbol arraySymbol:
                return new ArrayTypeDefinition(
                    arraySymbol.ElementType.GetTypeDefinition(), arraySymbol.Rank);

            // A type parameter names nothing outside its declaration, so it is never qualified
            // and contributes no namespace.
            case ITypeParameterSymbol typeParameterSymbol:
                return new TypeParameterDefinition(typeParameterSymbol.Name);

            case IDynamicTypeSymbol:
                return TypeDefinition.Get("", "dynamic");

            case IPointerTypeSymbol pointerSymbol:
                throw new NotSupportedException(
                    $"The type model has no pointer type, so {pointerSymbol.ToDisplayString()} cannot be " +
                    "converted. Write it with SyntaxHelpers rather than converting it, so that the " +
                    "gap is visible rather than silently producing the pointed-to type.");
        }

        if (typeSymbol is not INamedTypeSymbol namedTypeSymbol)
        {
            return TypeDefinition.Get(GetNamespace(typeSymbol), typeSymbol.Name);
        }

        // int? arrives as Nullable<int>. Treating it as an ordinary generic emits Nullable<int>,
        // which is correct C# and not what anyone writes.
        if (namedTypeSymbol.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T &&
            namedTypeSymbol.TypeArguments.Length == 1)
        {
            return namedTypeSymbol.TypeArguments[0].GetTypeDefinition().MakeNullable();
        }

        var typeNamespace = GetNamespace(namedTypeSymbol);

        var specialType = SpecialTypes.Get(typeNamespace, namedTypeSymbol.Name);

        if (specialType != null)
        {
            return specialType;
        }

        var containingType = namedTypeSymbol.ContainingType?.GetTypeDefinition();

        var definitionEnum = namedTypeSymbol.TypeKind switch
        {
            TypeKind.Interface => TypeDefinitionEnum.InterfaceDefinition,
            TypeKind.Enum => TypeDefinitionEnum.EnumDefinition,
            _ => TypeDefinitionEnum.ClassDefinition
        };

        // Arity, not IsGenericType: a type nested in a generic container reports IsGenericType
        // true while declaring no type parameters of its own, and building a generic from that
        // renders OuterGeneric<string>.Inner as OuterGeneric<string>.Inner<>. The container
        // carries the arguments; this type has none.
        if (namedTypeSymbol.Arity == 0)
        {
            return new TypeDefinition(
                definitionEnum, typeNamespace, namedTypeSymbol.Name, false, false, containingType);
        }

        var typeArguments = new List<ITypeDefinition>(namedTypeSymbol.TypeArguments.Length);

        foreach (var typeArgument in namedTypeSymbol.TypeArguments)
        {
            typeArguments.Add(typeArgument.GetTypeDefinition());
        }

        var genericDefinition = new GenericTypeDefinition(
            definitionEnum, typeNamespace, namedTypeSymbol.Name, typeArguments, false, false, containingType);

        // typeof(Dictionary<,>). The arguments are placeholders, so only their number means
        // anything - which is exactly what an open type keeps.
        return namedTypeSymbol.IsUnboundGenericType ? genericDefinition.MakeOpenType() : genericDefinition;
    }

    /// <summary>
    /// A symbol's namespace, with the global namespace as an empty string rather than the
    /// <c>&lt;global namespace&gt;</c> that <c>ToDisplayString</c> produces for it.
    /// </summary>
    public static string GetNamespace(this ISymbol symbol)
    {
        var containingNamespace = symbol?.ContainingNamespace;

        if (containingNamespace == null || containingNamespace.IsGlobalNamespace)
        {
            return "";
        }

        return containingNamespace.ToDisplayString();
    }

    /// <summary>
    /// The type a property, field, parameter or method return is declared as.
    /// </summary>
    public static ITypeDefinition GetTypeDefinition(this IPropertySymbol propertySymbol)
    {
        return propertySymbol.Type.GetTypeDefinition();
    }

    /// <inheritdoc cref="GetTypeDefinition(IPropertySymbol)"/>
    public static ITypeDefinition GetTypeDefinition(this IFieldSymbol fieldSymbol)
    {
        return fieldSymbol.Type.GetTypeDefinition();
    }

    /// <inheritdoc cref="GetTypeDefinition(IPropertySymbol)"/>
    public static ITypeDefinition GetTypeDefinition(this IParameterSymbol parameterSymbol)
    {
        return parameterSymbol.Type.GetTypeDefinition();
    }

    /// <inheritdoc cref="GetTypeDefinition(IPropertySymbol)"/>
    public static ITypeDefinition GetReturnTypeDefinition(this IMethodSymbol methodSymbol)
    {
        return methodSymbol.ReturnType.GetTypeDefinition();
    }
}
