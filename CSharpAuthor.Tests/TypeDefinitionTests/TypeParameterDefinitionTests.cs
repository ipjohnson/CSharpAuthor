using System.Text;
using Xunit;

namespace CSharpAuthor.Tests.TypeDefinitionTests;

public class TypeParameterDefinitionTests
{
    /// <summary>
    /// A type parameter names nothing outside its declaration, so it is written as itself even
    /// where a real type would be qualified.
    /// </summary>
    [Theory]
    [InlineData(TypeOutputMode.ShortName)]
    [InlineData(TypeOutputMode.FullName)]
    [InlineData(TypeOutputMode.Global)]
    public void WrittenUnqualifiedInEveryMode(TypeOutputMode mode)
    {
        var builder = new StringBuilder();

        new TypeParameterDefinition("T").WriteTypeName(builder, mode);

        Assert.Equal("T", builder.ToString());
    }

    [Fact]
    public void NullableAndArray()
    {
        var builder = new StringBuilder();

        new TypeParameterDefinition("T").MakeNullable().WriteTypeName(builder);

        Assert.Equal("T?", builder.ToString());

        builder.Clear();

        new TypeParameterDefinition("T").MakeArray().WriteTypeName(builder);

        Assert.Equal("T[]", builder.ToString());
    }

    /// <summary>
    /// Value equality matters to a source generator: it caches on models holding these, and
    /// reference equality would miss that cache on every edit.
    /// </summary>
    [Fact]
    public void EqualByValue()
    {
        Assert.Equal(new TypeParameterDefinition("T"), new TypeParameterDefinition("T"));
        Assert.Equal(new TypeParameterDefinition("T").GetHashCode(), new TypeParameterDefinition("T").GetHashCode());

        Assert.NotEqual(new TypeParameterDefinition("T"), new TypeParameterDefinition("U"));
        Assert.NotEqual(new TypeParameterDefinition("T"), (ITypeDefinition)TypeDefinition.Get("Ns", "T"));
    }

    [Fact]
    public void ClosesAGenericType()
    {
        var builder = new StringBuilder();

        new GenericTypeDefinition(
                TypeDefinitionEnum.ClassDefinition,
                "Ns",
                "Container",
                new ITypeDefinition[] { new TypeParameterDefinition("T") })
            .WriteTypeName(builder, TypeOutputMode.Global);

        Assert.Equal("global::Ns.Container<T>", builder.ToString());
    }
}
