using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace CSharpAuthor.Roslyn;

/// <summary>
/// Turns Roslyn's <c>ITypeSymbol</c> into the type model the writers emit from.
/// </summary>
/// <remarks>
/// <para>
/// Every source generator built on this library starts here, and until now every one of them wrote
/// this conversion itself. The two that exist agree on the easy half and are both wrong in the same
/// places: a nested type inside a closed generic loses its container's arguments, a rank-2 array
/// becomes a rank-1 one, <c>float</c> comes out as <c>Single</c>, and a non-generic type nested in a
/// generic one is treated as generic and emitted with an empty argument list.
/// </para>
/// <para>
/// The conversion returns the plainest type the model can express: a <c>TypeDefinition</c> for a
/// simple name, a <c>GenericTypeDefinition</c> for a closed generic, the same dotted name for a
/// nested type that both consumers already build. Where the model cannot say what the symbol means
/// — a rank, a jagged rank order, a container's own arguments, a tuple's element names — the
/// conversion returns a type that can, and those types render through the same deferred
/// <c>WriteTypeName</c> as everything else, so a file can still be flipped between short names and
/// <c>global::</c> after the fact.
/// </para>
/// </remarks>
public static class SymbolTypeExtensions
{
    /// <summary>
    /// The type definition for a symbol, whatever kind of type it is.
    /// </summary>
    public static ITypeDefinition GetTypeDefinition(this ITypeSymbol typeSymbol)
    {
        if (typeSymbol == null)
        {
            throw new ArgumentNullException(nameof(typeSymbol));
        }

        return Convert(typeSymbol);
    }

    /// <summary>
    /// The type definition for a syntax node — a type reference, or an expression whose type is
    /// wanted.
    /// </summary>
    /// <remarks>
    /// Reads the annotation off the semantic model rather than off the text. Asking whether the
    /// source ended in "?" gets <c>List&lt;string?&gt;</c> wrong, and gets a type alias or an
    /// inferred type wrong in both directions.
    /// </remarks>
    public static ITypeDefinition? GetTypeDefinition(this SyntaxNode node, SemanticModel semanticModel)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (semanticModel == null)
        {
            throw new ArgumentNullException(nameof(semanticModel));
        }

        var typeInfo = semanticModel.GetTypeInfo(node);

        var type = typeInfo.Type;

        if (type == null)
        {
            type = semanticModel.GetSymbolInfo(node).Symbol as ITypeSymbol;
        }

        return type == null ? null : Convert(type);
    }

    /// <summary>The type definition a bound symbol refers to, or null when it is not a type.</summary>
    public static ITypeDefinition? GetTypeDefinition(this SymbolInfo symbolInfo)
    {
        return symbolInfo.Symbol is ITypeSymbol typeSymbol ? Convert(typeSymbol) : null;
    }

    /// <summary>
    /// The dotted namespace name, with the global namespace answering the empty string.
    /// </summary>
    public static string GetFullNamespace(this INamespaceSymbol? namespaceSymbol)
    {
        if (namespaceSymbol == null || namespaceSymbol.IsGlobalNamespace)
        {
            return "";
        }

        var containing = GetFullNamespace(namespaceSymbol.ContainingNamespace);

        return string.IsNullOrEmpty(containing) ? namespaceSymbol.Name : containing + "." + namespaceSymbol.Name;
    }

    /// <summary>
    /// Whether the type is <c>Nullable&lt;T&gt;</c> rather than a reference type carrying a nullable
    /// annotation. Both render a trailing <c>?</c>; only one of them is a different type.
    /// </summary>
    public static bool IsNullableValueType(this ITypeDefinition typeDefinition)
    {
        return typeDefinition is NullableValueTypeDefinition;
    }

    private static ITypeDefinition Convert(ITypeSymbol symbol)
    {
        switch (symbol)
        {
            case IArrayTypeSymbol arrayType:
                return ConvertArray(arrayType);

            case IPointerTypeSymbol pointerType:
                return new PointerTypeDefinition(Convert(pointerType.PointedAtType));

            case IFunctionPointerTypeSymbol functionPointerType:
                return ConvertFunctionPointer(functionPointerType);

            case IDynamicTypeSymbol:
                return Annotate(TypeDefinition.Get(TypeDefinitionEnum.ClassDefinition, "", "dynamic"), symbol);

            case ITypeParameterSymbol typeParameter:
                return Annotate(new TypeParameterDefinition(RoslynSyntaxFacts.Escape(typeParameter.Name)), symbol);

            case INamedTypeSymbol namedType:
                return ConvertNamed(namedType);

            default:
                return Annotate(
                    TypeDefinition.Get(
                        KindOf(symbol),
                        GetFullNamespace(symbol.ContainingNamespace),
                        RoslynSyntaxFacts.Escape(symbol.Name)),
                    symbol);
        }
    }

    private static ITypeDefinition ConvertNamed(INamedTypeSymbol symbol)
    {
        if (symbol.IsTupleType)
        {
            return ConvertTuple(symbol);
        }

        if (!symbol.IsUnboundGenericType &&
            symbol.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            symbol.TypeArguments.Length == 1)
        {
            return new NullableValueTypeDefinition(Convert(symbol.TypeArguments[0]));
        }

        var keyword = RoslynSyntaxFacts.Keyword(symbol.SpecialType);

        if (keyword != null)
        {
            return Annotate(TypeDefinition.Get(KindOf(symbol), "", keyword), symbol);
        }

        if (symbol.SpecialType == SpecialType.System_Void)
        {
            // The type model already writes System.Void as void, and a caller comparing against
            // TypeDefinition.Get(typeof(void)) is comparing against this exact pair.
            return TypeDefinition.Get(TypeDefinitionEnum.ClassDefinition, "System", "Void");
        }

        var containers = new List<INamedTypeSymbol>();

        for (INamedTypeSymbol? link = symbol; link != null; link = link.ContainingType)
        {
            containers.Insert(0, link);
        }

        var unbound = symbol.IsUnboundGenericType;
        var containerIsClosed = false;

        for (var i = 0; i < containers.Count - 1; i++)
        {
            if (containers[i].TypeArguments.Length > 0)
            {
                containerIsClosed = true;
            }
        }

        var ns = GetFullNamespace(symbol.ContainingNamespace);

        if (!containerIsClosed)
        {
            var name = DottedName(containers);
            var typeArguments = TypeArgumentsOf(symbol, unbound);

            if (typeArguments.Count == 0)
            {
                return Annotate(TypeDefinition.Get(KindOf(symbol), ns, name), symbol);
            }

            return Annotate(new GenericTypeDefinition(KindOf(symbol), ns, name, typeArguments), symbol);
        }

        var segments = new List<NestedTypeSegment>(containers.Count);

        foreach (var link in containers)
        {
            segments.Add(new NestedTypeSegment(
                RoslynSyntaxFacts.Escape(link.Name),
                TypeArgumentsOf(link, unbound)));
        }

        return Annotate(new NestedTypeDefinition(KindOf(symbol), ns, segments), symbol);
    }

    private static ITypeDefinition ConvertTuple(INamedTypeSymbol symbol)
    {
        var tupleElements = symbol.TupleElements;

        var elements = new List<TupleElementDefinition>(tupleElements.Length);

        foreach (var element in tupleElements)
        {
            elements.Add(new TupleElementDefinition(
                Convert(element.Type),
                element.IsImplicitlyDeclared ? null : RoslynSyntaxFacts.Escape(element.Name)));
        }

        if (elements.Count < 2)
        {
            // A tuple symbol always has its elements; a malformed one is still worth a type rather
            // than an exception out of a generator.
            foreach (var typeArgument in symbol.TypeArguments)
            {
                elements.Add(new TupleElementDefinition(Convert(typeArgument)));
            }
        }

        return Annotate(new TupleTypeDefinition(elements), symbol);
    }

    private static ITypeDefinition ConvertArray(IArrayTypeSymbol symbol)
    {
        var annotated = symbol.NullableAnnotation == NullableAnnotation.Annotated;

        if (symbol.Rank == 1 && !annotated && symbol.ElementType is not IArrayTypeSymbol)
        {
            var element = Convert(symbol.ElementType);

            // MakeArray on a nullable type sets the array's own bit, which renders T[]? for what was
            // written T?[]. Those are different types, so a nullable element keeps its own level.
            if (!element.IsNullable)
            {
                return element.MakeArray();
            }

            return new ArrayTypeDefinition(element, 1);
        }

        return new ArrayTypeDefinition(ConvertArrayLevel(symbol.ElementType), symbol.Rank, annotated);
    }

    /// <summary>
    /// Converts an element that sits under another array level.
    /// </summary>
    /// <remarks>
    /// Every level of a multi-level array has to stay a level. The flattened form the type model
    /// uses for <c>T[]</c> cannot be taken apart again, and the ranks of <c>int[,][]</c> have to be
    /// written outermost-first — so folding the inner one away emits <c>int[][,]</c>, which is a
    /// different type that happens to compile.
    /// </remarks>
    private static ITypeDefinition ConvertArrayLevel(ITypeSymbol symbol)
    {
        if (symbol is IArrayTypeSymbol arrayType)
        {
            return new ArrayTypeDefinition(
                ConvertArrayLevel(arrayType.ElementType),
                arrayType.Rank,
                arrayType.NullableAnnotation == NullableAnnotation.Annotated);
        }

        return Convert(symbol);
    }

    private static ITypeDefinition ConvertFunctionPointer(IFunctionPointerTypeSymbol symbol)
    {
        var signature = symbol.Signature;

        var parameterTypes = new List<ITypeDefinition>(signature.Parameters.Length);

        foreach (var parameter in signature.Parameters)
        {
            parameterTypes.Add(Convert(parameter.Type));
        }

        return new FunctionPointerTypeDefinition(
            parameterTypes,
            Convert(signature.ReturnType),
            CallingConventionOf(signature));
    }

    private static string? CallingConventionOf(IMethodSymbol signature)
    {
        var convention = signature.CallingConvention.ToString();

        if (string.Equals(convention, "Default", StringComparison.Ordinal))
        {
            return null;
        }

        var builder = new StringBuilder("unmanaged");

        var conventionTypes = signature.UnmanagedCallingConventionTypes;

        if (conventionTypes.Length > 0)
        {
            builder.Append('[');

            for (var i = 0; i < conventionTypes.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                var name = conventionTypes[i].Name;

                builder.Append(name.StartsWith("CallConv", StringComparison.Ordinal) ? name.Substring(8) : name);
            }

            builder.Append(']');
        }
        else if (!string.Equals(convention, "Unmanaged", StringComparison.Ordinal))
        {
            builder.Append('[');
            builder.Append(NormalizeConventionName(convention));
            builder.Append(']');
        }

        return builder.ToString();
    }

    /// <summary>The metadata spelling of a calling convention is not the C# one.</summary>
    private static string NormalizeConventionName(string convention)
    {
        switch (convention)
        {
            case "CDecl": return "Cdecl";
            case "StdCall": return "Stdcall";
            case "ThisCall": return "Thiscall";
            case "FastCall": return "Fastcall";
            default: return convention;
        }
    }

    private static IReadOnlyList<ITypeDefinition> TypeArgumentsOf(INamedTypeSymbol symbol, bool unbound)
    {
        if (symbol.TypeArguments.Length == 0)
        {
            return Array.Empty<ITypeDefinition>();
        }

        var typeArguments = new ITypeDefinition[symbol.TypeArguments.Length];

        for (var i = 0; i < symbol.TypeArguments.Length; i++)
        {
            // typeof(List<>) binds to the unbound symbol, whose arguments are the declaration's own
            // type parameters. Writing them out produces typeof(List<T>), where T is not in scope.
            typeArguments[i] = unbound
                ? TypeDefinition.Get("", "")
                : Convert(symbol.TypeArguments[i]);
        }

        return typeArguments;
    }

    private static string DottedName(IReadOnlyList<INamedTypeSymbol> containers)
    {
        if (containers.Count == 1)
        {
            return RoslynSyntaxFacts.Escape(containers[0].Name);
        }

        var builder = new StringBuilder();

        for (var i = 0; i < containers.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('.');
            }

            builder.Append(RoslynSyntaxFacts.Escape(containers[i].Name));
        }

        return builder.ToString();
    }

    private static ITypeDefinition Annotate(ITypeDefinition typeDefinition, ITypeSymbol symbol)
    {
        return symbol.NullableAnnotation == NullableAnnotation.Annotated
            ? typeDefinition.MakeNullable()
            : typeDefinition;
    }

    private static TypeDefinitionEnum KindOf(ITypeSymbol symbol)
    {
        switch (symbol.TypeKind)
        {
            case TypeKind.Enum: return TypeDefinitionEnum.EnumDefinition;
            case TypeKind.Interface: return TypeDefinitionEnum.InterfaceDefinition;
            default: return TypeDefinitionEnum.ClassDefinition;
        }
    }
}
