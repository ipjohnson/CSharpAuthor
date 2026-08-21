using CSharpAuthor.Roslyn;
using Xunit;

namespace CSharpAuthor.Tests.RoslynBridgeTests;

/// <summary>Constructed generics, open generics, and type parameters.</summary>
public class GenericConversionTests
{
    private const string Generics = @"
        public List<int> list;
        public Dictionary<string, int> dictionary;
        public List<Dictionary<string, int?>> nestedArguments;
        public List<List<List<string>>> deeplyNested;
        public T typeParameter;
        public T[] typeParameterArray;
        public List<T> listOfTypeParameter;
        public IThing interfaceType;
        public Color enumType;
";

    [Theory]
    [InlineData("list", "List<int>")]
    [InlineData("dictionary", "Dictionary<string,int>")]
    [InlineData("nestedArguments", "List<Dictionary<string,int?>>")]
    [InlineData("deeplyNested", "List<List<List<string>>>")]
    [InlineData("typeParameter", "T")]
    [InlineData("typeParameterArray", "T[]")]
    [InlineData("listOfTypeParameter", "List<T>")]
    public void ConstructedGenericsKeepTheirArguments(string field, string expected)
    {
        var typeDefinition = TestCompilation.FieldType(Generics, field).GetTypeDefinition();

        Assert.Equal(expected, TestCompilation.Write(typeDefinition));
    }

    /// <summary>
    /// The kind survives, because an interface and an enum are written differently everywhere else.
    /// </summary>
    [Fact]
    public void TypeKindIsCarried()
    {
        var types = TestCompilation.FieldTypes(Generics);

        Assert.Equal(TypeDefinitionEnum.InterfaceDefinition, types["interfaceType"].GetTypeDefinition().TypeDefinitionEnum);
        Assert.Equal(TypeDefinitionEnum.EnumDefinition, types["enumType"].GetTypeDefinition().TypeDefinitionEnum);
        Assert.Equal(TypeDefinitionEnum.ClassDefinition, types["list"].GetTypeDefinition().TypeDefinitionEnum);
    }

    /// <summary>
    /// A type parameter names nothing outside its declaration: no namespace, and no qualification in
    /// any output mode.
    /// </summary>
    [Fact]
    public void TypeParameterIsWrittenAsItself()
    {
        var typeDefinition = TestCompilation.FieldType(Generics, "typeParameter").GetTypeDefinition();

        Assert.IsType<TypeParameterDefinition>(typeDefinition);
        Assert.Equal("", typeDefinition.Namespace);
        Assert.Equal("T", TestCompilation.Write(typeDefinition, TypeOutputMode.Global));
        Assert.Empty(typeDefinition.KnownNamespaces);
    }

    /// <summary>
    /// <c>typeof(List&lt;&gt;)</c> binds to the unbound symbol, whose arguments are the
    /// declaration's own type parameters. Writing them out produces <c>typeof(List&lt;T&gt;)</c>,
    /// where <c>T</c> is not in scope.
    /// </summary>
    [Theory]
    [InlineData("List<>", "List<>")]
    [InlineData("Dictionary<,>", "Dictionary<,>")]
    [InlineData("BridgeTestNamespace.Outer<>", "Outer<>")]
    [InlineData("BridgeTestNamespace.Outer<>.Inner<>", "Outer<>.Inner<>")]
    public void UnboundGenericsAreWrittenOpen(string typeExpression, string expected)
    {
        var typeDefinition = TestCompilation.TypeOfArgument(typeExpression).GetTypeDefinition();

        Assert.Equal(expected, TestCompilation.Write(typeDefinition));
    }

    [Fact]
    public void UnboundGenericQualifiesInGlobalMode()
    {
        var typeDefinition = TestCompilation.TypeOfArgument("List<>").GetTypeDefinition();

        Assert.Equal(
            "global::System.Collections.Generic.List<>",
            TestCompilation.Write(typeDefinition, TypeOutputMode.Global));
    }

    /// <summary>Every argument's namespace reaches the file, however deeply nested it is.</summary>
    [Fact]
    public void NestedArgumentsContributeTheirNamespaces()
    {
        var source = @"
        public List<System.Threading.Tasks.Task<System.Text.StringBuilder>> deep;
";

        var typeDefinition = TestCompilation.FieldType(source, "deep").GetTypeDefinition();

        Assert.Contains("System.Collections.Generic", typeDefinition.KnownNamespaces);
        Assert.Contains("System.Threading.Tasks", typeDefinition.KnownNamespaces);
        Assert.Contains("System.Text", typeDefinition.KnownNamespaces);
    }

    /// <summary>
    /// A constructed generic converts to the shape the type model has always used, so it still
    /// compares equal to a hand-built one.
    /// </summary>
    [Fact]
    public void ConstructedGenericKeepsTheOriginalShape()
    {
        var typeDefinition = TestCompilation.FieldType(Generics, "list").GetTypeDefinition();

        Assert.IsType<GenericTypeDefinition>(typeDefinition);

        Assert.True(typeDefinition.Equals(
            new GenericTypeDefinition(
                TypeDefinitionEnum.ClassDefinition,
                "System.Collections.Generic",
                "List",
                new[] { TypeDefinition.Get(typeof(int)) })));
    }
}
