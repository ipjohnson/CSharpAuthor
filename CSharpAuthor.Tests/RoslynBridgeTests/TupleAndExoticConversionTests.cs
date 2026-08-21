using CSharpAuthor.Roslyn;
using Xunit;

namespace CSharpAuthor.Tests.RoslynBridgeTests;

/// <summary>
/// Tuples, <c>dynamic</c>, pointers and function pointers: types with no name to fall back on.
/// </summary>
public class TupleAndExoticConversionTests
{
    private const string Exotics = @"
        public (int a, string b) named;
        public (int, string) positional;
        public (int a, (string x, bool y) b) nestedTuple;
        public (int a, string b)? nullableTuple;
        public (int, string)[] tupleArray;
        public (List<int> items, string name) tupleOfGeneric;
        public (int a, string b, bool c, double d, long e, short f, byte g, char h) longTuple;
        public dynamic dynamicField;
        public dynamic? nullableDynamic;
        public List<dynamic> listOfDynamic;
        public int* pointer;
        public void* voidPointer;
        public int** pointerToPointer;
        public int*[] pointerArray;
        public delegate*<int, void> functionPointer;
        public delegate*<int, string, bool> functionPointerWithReturn;
        public delegate* unmanaged[Cdecl]<int, int> unmanagedFunctionPointer;
";

    [Theory]
    [InlineData("named", "(int a, string b)")]
    [InlineData("positional", "(int, string)")]
    [InlineData("nestedTuple", "(int a, (string x, bool y) b)")]
    [InlineData("nullableTuple", "(int a, string b)?")]
    [InlineData("tupleArray", "(int, string)[]")]
    [InlineData("tupleOfGeneric", "(List<int> items, string name)")]
    [InlineData("longTuple", "(int a, string b, bool c, double d, long e, short f, byte g, char h)")]
    [InlineData("dynamicField", "dynamic")]
    [InlineData("nullableDynamic", "dynamic?")]
    [InlineData("listOfDynamic", "List<dynamic>")]
    [InlineData("pointer", "int*")]
    [InlineData("voidPointer", "void*")]
    [InlineData("pointerToPointer", "int**")]
    [InlineData("pointerArray", "int*[]")]
    [InlineData("functionPointer", "delegate*<int, void>")]
    [InlineData("functionPointerWithReturn", "delegate*<int, string, bool>")]
    [InlineData("unmanagedFunctionPointer", "delegate* unmanaged[Cdecl]<int, int>")]
    public void ExoticTypesAreWrittenAsTheyWereDeclared(string field, string expected)
    {
        var typeDefinition = TestCompilation.FieldType(Exotics, field).GetTypeDefinition();

        Assert.Equal(expected, TestCompilation.Write(typeDefinition));
    }

    /// <summary>
    /// Element names are part of the type as written and are not recoverable from the underlying
    /// <c>ValueTuple</c>, so the conversion is the only place they can be kept.
    /// </summary>
    [Fact]
    public void TupleElementNamesAreKept()
    {
        var tuple = Assert.IsType<TupleTypeDefinition>(
            TestCompilation.FieldType(Exotics, "named").GetTypeDefinition());

        Assert.Equal(2, tuple.Elements.Count);
        Assert.Equal("a", tuple.Elements[0].Name);
        Assert.Equal("b", tuple.Elements[1].Name);
        Assert.Equal("int", TestCompilation.Write(tuple.Elements[0].Type));
    }

    /// <summary>A positional tuple has no names, and <c>Item1</c> is not one.</summary>
    [Fact]
    public void PositionalTupleHasNoElementNames()
    {
        var tuple = Assert.IsType<TupleTypeDefinition>(
            TestCompilation.FieldType(Exotics, "positional").GetTypeDefinition());

        Assert.Null(tuple.Elements[0].Name);
        Assert.Null(tuple.Elements[1].Name);
    }

    /// <summary>
    /// Tuple syntax is built in, so the tuple contributes no import of its own - only what its
    /// elements need.
    /// </summary>
    [Fact]
    public void TupleContributesOnlyItsElementNamespaces()
    {
        var typeDefinition = TestCompilation.FieldType(Exotics, "tupleOfGeneric").GetTypeDefinition();

        Assert.Contains("System.Collections.Generic", typeDefinition.KnownNamespaces);
        Assert.DoesNotContain("System", typeDefinition.KnownNamespaces);
    }

    [Fact]
    public void TupleElementsQualifyInGlobalMode()
    {
        var typeDefinition = TestCompilation.FieldType(Exotics, "tupleOfGeneric").GetTypeDefinition();

        Assert.Equal(
            "(global::System.Collections.Generic.List<int> items, string name)",
            TestCompilation.Write(typeDefinition, TypeOutputMode.Global));
    }

    /// <summary><c>dynamic</c> is a keyword with no namespace, not a type named dynamic.</summary>
    [Fact]
    public void DynamicNeedsNoImport()
    {
        var typeDefinition = TestCompilation.FieldType(Exotics, "dynamicField").GetTypeDefinition();

        Assert.Equal("", typeDefinition.Namespace);
        Assert.Equal("dynamic", TestCompilation.Write(typeDefinition, TypeOutputMode.Global));
    }

    [Fact]
    public void PointerKeepsItsPointeeQualified()
    {
        var source = @"
        public Val* structPointer;
";

        var typeDefinition = TestCompilation.FieldType(source, "structPointer").GetTypeDefinition();

        Assert.IsType<PointerTypeDefinition>(typeDefinition);
        Assert.Equal("global::BridgeTestNamespace.Val*", TestCompilation.Write(typeDefinition, TypeOutputMode.Global));
        Assert.Contains("BridgeTestNamespace", typeDefinition.KnownNamespaces);
    }

    [Fact]
    public void FunctionPointerKeepsParametersAndReturn()
    {
        var functionPointer = Assert.IsType<FunctionPointerTypeDefinition>(
            TestCompilation.FieldType(Exotics, "functionPointerWithReturn").GetTypeDefinition());

        Assert.Equal(2, functionPointer.ParameterTypes.Count);
        Assert.Equal("bool", TestCompilation.Write(functionPointer.ReturnType));
    }

    /// <summary>
    /// A type whose name is a keyword is written escaped, because the symbol reports the identifier
    /// and the identifier alone does not compile.
    /// </summary>
    [Fact]
    public void KeywordNamedTypesAreEscaped()
    {
        var source = @"
        public @event keywordType;
";

        var typeDefinition = TestCompilation.FieldType(source, "keywordType").GetTypeDefinition();

        Assert.Equal("@event", TestCompilation.Write(typeDefinition));
        Assert.Equal("global::BridgeTestNamespace.@event", TestCompilation.Write(typeDefinition, TypeOutputMode.Global));
    }

    /// <summary>A type the compiler could not resolve still converts, rather than throwing.</summary>
    [Fact]
    public void UnresolvedTypesConvertToTheirName()
    {
        var compilation = TestCompilation.Compile(TestCompilation.Wrap("public NoSuchType missing;"));

        var holder = compilation.GetTypeByMetadataName("BridgeTestNamespace.Holder`1");

        Assert.NotNull(holder);

        foreach (var member in holder!.GetMembers("missing"))
        {
            if (member is Microsoft.CodeAnalysis.IFieldSymbol field)
            {
                Assert.Equal("NoSuchType", TestCompilation.Write(field.Type.GetTypeDefinition()));
            }
        }
    }
}
