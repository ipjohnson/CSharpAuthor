using System.Collections.Generic;
using Xunit;

namespace CSharpAuthor.Tests.TypeDefinitionTests;

public class GenericTypeArgumentTests
{
    private static GenericTypeDefinition Dictionary() =>
        new(
            TypeDefinitionEnum.ClassDefinition,
            "System.Collections.Generic",
            "Dictionary",
            new List<ITypeDefinition>
            {
                TypeDefinition.Get(typeof(string)),
                TypeDefinition.Get(typeof(int))
            });

    [Fact]
    public void TypeArgumentsAreSeparatedByCommaSpace()
    {
        var context = new OutputContext();

        context.Write(Dictionary());

        Assert.Equal("Dictionary<string, int>", context.Output());
    }

    /// <summary>
    /// Openness used to be faked with blank type arguments, each of which still wrote the <c>.</c>
    /// joining its namespace to its name - so an open type rendered as
    /// <c>Dictionary&lt;.,.&gt;</c>.
    /// </summary>
    [Fact]
    public void OpenTypeWritesCommasWithoutArguments()
    {
        var context = new OutputContext();

        context.Write(Dictionary().MakeOpenType());

        Assert.Equal("Dictionary<,>", context.Output());
    }

    [Fact]
    public void OpenTypeImportsOnlyItsOwnNamespace()
    {
        var context = new OutputContext();

        context.Write(Dictionary().MakeOpenType());
        context.GenerateUsingStatements();

        Assert.Equal("using System.Collections.Generic;\n\nDictionary<,>", context.Output());
    }

    [Fact]
    public void OpenTypeSurvivesBeingMadeNullable()
    {
        var context = new OutputContext();

        context.Write(Dictionary().MakeOpenType().MakeNullable());

        Assert.Equal("Dictionary<,>?", context.Output());
    }
}
