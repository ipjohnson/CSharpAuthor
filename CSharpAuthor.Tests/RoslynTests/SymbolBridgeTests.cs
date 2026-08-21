using System.Collections.Generic;
using System.Linq;
using CSharpAuthor.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace CSharpAuthor.Tests.RoslynTests;

/// <summary>
/// Driven from real compiled source rather than hand-built symbols, because the cases that used to
/// be got wrong - nested types, <c>Nullable&lt;T&gt;</c>, jagged and multidimensional arrays - are
/// exactly the ones where a hand-built symbol would encode the assumption being tested.
/// </summary>
public class SymbolBridgeTests
{
    private const string Source = @"
using System;
using System.Collections.Generic;

namespace Sample.Models
{
    public class Outer
    {
        public class Inner { }
    }

    public class OuterGeneric<T>
    {
        public class Inner { }
    }

    public interface IMarker { }

    public enum Colour { Red }

    public struct Point { }

    public class Holder<TValue>
    {
        public int Number { get; set; }
        public float Ratio { get; set; }
        public char Letter { get; set; }
        public sbyte Small { get; set; }
        public IntPtr Native { get; set; }
        public int? MaybeNumber { get; set; }
        public string Text { get; set; }
        public string? MaybeText { get; set; }
        public string[] Names { get; set; }
        public string[][] Jagged { get; set; }
        public int[,] Grid { get; set; }
        public int[][,] Mixed { get; set; }
        public Dictionary<string, int> Map { get; set; }
        public Outer.Inner Nested { get; set; }
        public OuterGeneric<string>.Inner NestedInGeneric { get; set; }
        public IMarker Marker { get; set; }
        public Colour Shade { get; set; }
        public Point Location { get; set; }
        public TValue Value { get; set; }
        public TValue[] Values { get; set; }
        public List<Outer.Inner> NestedList { get; set; }
        public void Go(ref int counter) { }
    }
}";

    private static readonly INamedTypeSymbol _holder = CompileHolder();

    private static INamedTypeSymbol CompileHolder()
    {
        var compilation = CSharpCompilation.Create(
            "SymbolBridgeTests",
            new[] { CSharpSyntaxTree.ParseText(Source) },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        // A bridge fed error symbols would be testing the wrong thing.
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.Empty(errors);

        return compilation.GetTypeByMetadataName("Sample.Models.Holder`1")!;
    }

    private static string Render(string propertyName, TypeOutputMode mode = TypeOutputMode.ShortName)
    {
        var property = (IPropertySymbol)_holder.GetMembers(propertyName).Single();

        var builder = new System.Text.StringBuilder();

        property.GetTypeDefinition().WriteTypeName(builder, mode);

        return builder.ToString();
    }

    [Theory]
    [InlineData("Number", "int")]
    [InlineData("Ratio", "float")]
    [InlineData("Letter", "char")]
    [InlineData("Small", "sbyte")]
    [InlineData("Text", "string")]
    public void FrameworkTypesBecomeKeywords(string property, string expected)
    {
        Assert.Equal(expected, Render(property));
    }

    /// <summary>
    /// C# 9, so it stays as the framework name until a target version reaches rendering.
    /// </summary>
    [Fact]
    public void NativeIntegerKeepsItsFrameworkName()
    {
        Assert.Equal("IntPtr", Render("Native"));
    }

    [Fact]
    public void NullableValueTypeUsesTheShorthand()
    {
        Assert.Equal("int?", Render("MaybeNumber"));
    }

    [Fact]
    public void NullableReferenceAnnotationIsCarried()
    {
        Assert.Equal("string?", Render("MaybeText"));
        Assert.Equal("string", Render("Text"));
    }

    [Theory]
    [InlineData("Names", "string[]")]
    [InlineData("Jagged", "string[][]")]
    [InlineData("Grid", "int[,]")]
    [InlineData("Mixed", "int[,][]")]
    public void ArraysKeepRankAndNesting(string property, string expected)
    {
        Assert.Equal(expected, Render(property));
    }

    [Fact]
    public void GenericsCarryTheirArguments()
    {
        Assert.Equal("Dictionary<string, int>", Render("Map"));
    }

    /// <summary>
    /// Without the container, <c>Inner</c> binds to whatever <c>Inner</c> is in scope at the point
    /// of use - or to nothing.
    /// </summary>
    [Fact]
    public void NestedTypesKeepTheirContainer()
    {
        Assert.Equal("Outer.Inner", Render("Nested"));
        Assert.Equal("global::Sample.Models.Outer.Inner", Render("Nested", TypeOutputMode.Global));
    }

    [Fact]
    public void NestedInsideAGenericContainerKeepsItsArguments()
    {
        Assert.Equal("OuterGeneric<string>.Inner", Render("NestedInGeneric"));
    }

    [Fact]
    public void NestedTypeInsideAGenericArgumentStillQualifies()
    {
        Assert.Equal("List<Outer.Inner>", Render("NestedList"));
    }

    [Fact]
    public void TypeKindIsCarried()
    {
        var marker = (IPropertySymbol)_holder.GetMembers("Marker").Single();
        var shade = (IPropertySymbol)_holder.GetMembers("Shade").Single();
        var location = (IPropertySymbol)_holder.GetMembers("Location").Single();

        Assert.Equal(TypeDefinitionEnum.InterfaceDefinition, marker.GetTypeDefinition().TypeDefinitionEnum);
        Assert.Equal(TypeDefinitionEnum.EnumDefinition, shade.GetTypeDefinition().TypeDefinitionEnum);
        Assert.Equal(TypeDefinitionEnum.ClassDefinition, location.GetTypeDefinition().TypeDefinitionEnum);
    }

    /// <summary>
    /// A type parameter names nothing outside its declaration, so it is never qualified and
    /// contributes no namespace.
    /// </summary>
    [Fact]
    public void TypeParametersAreWrittenAsThemselves()
    {
        Assert.Equal("TValue", Render("Value", TypeOutputMode.Global));
        Assert.Equal("TValue[]", Render("Values"));

        var value = (IPropertySymbol)_holder.GetMembers("Value").Single();

        Assert.Empty(value.GetTypeDefinition().KnownNamespaces.Where(ns => !string.IsNullOrEmpty(ns)));
    }

    [Fact]
    public void ParameterAndReturnTypesConvert()
    {
        var method = (IMethodSymbol)_holder.GetMembers("Go").Single();

        Assert.Equal("void", method.GetReturnTypeDefinition().GetShortName());
        Assert.Equal("int", method.Parameters[0].GetTypeDefinition().GetShortName());
    }

    /// <summary>
    /// The whole point of converting rather than stringifying: the type stays unrendered, so the
    /// output context still derives the imports.
    /// </summary>
    [Fact]
    public void ConvertedTypesStillDeriveImports()
    {
        var property = (IPropertySymbol)_holder.GetMembers("Map").Single();

        var context = new OutputContext();

        context.Write(property.GetTypeDefinition());
        context.GenerateUsingStatements();

        Assert.Equal("using System.Collections.Generic;\n\nDictionary<string, int>", context.Output());
    }

    [Fact]
    public void KeywordsAndTypeParametersImportNothing()
    {
        var context = new OutputContext();

        context.Write(((IPropertySymbol)_holder.GetMembers("Number").Single()).GetTypeDefinition());
        context.Write(((IPropertySymbol)_holder.GetMembers("Value").Single()).GetTypeDefinition());
        context.GenerateUsingStatements();

        Assert.Equal("intTValue", context.Output());
    }

    [Fact]
    public void UnboundGenericBecomesAnOpenType()
    {
        var map = (IPropertySymbol)_holder.GetMembers("Map").Single();
        var unbound = ((INamedTypeSymbol)map.Type).ConstructUnboundGenericType();

        Assert.Equal("Dictionary<,>", unbound.GetTypeDefinition().GetShortName());
    }

    [Fact]
    public void GlobalNamespaceIsEmptyRatherThanItsDisplayString()
    {
        Assert.Equal("Sample.Models", _holder.GetNamespace());
        Assert.Equal("", _holder.ContainingNamespace.ContainingNamespace.ContainingNamespace.GetNamespace());
    }

    /// <summary>
    /// A pointer has no representation in the type model, so it says so rather than quietly
    /// producing the pointed-to type.
    /// </summary>
    [Fact]
    public void PointersAreRefusedRatherThanApproximated()
    {
        var compilation = CSharpCompilation.Create(
            "Pointers",
            new[] { CSharpSyntaxTree.ParseText("public unsafe class P { public int* Value; }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        var field = (IFieldSymbol)compilation.GetTypeByMetadataName("P")!.GetMembers("Value").Single();

        Assert.Throws<System.NotSupportedException>(() => field.GetTypeDefinition());
    }
}
