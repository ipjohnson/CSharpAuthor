using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace CSharpAuthor.Tests.TypeDefinitionTests;

/// <summary>
/// A type stays unrendered until the file is written, so one option flips every type in it between
/// short names and <c>global::</c>. Anything that takes a type's name while building the tree has
/// already decided, and has thrown away the generic arguments, the array shape and the containing
/// type along the way.
/// </summary>
public class DeferredTypeWritingTests
{
    private static string Write(IOutputComponent component, TypeOutputMode mode)
    {
        var context = new OutputContext(new OutputContextOptions { TypeOutputMode = mode });

        component.WriteOutput(context);

        return context.Output();
    }

    private static string WriteWithUsings(IOutputComponent component, TypeOutputMode mode)
    {
        var context = new OutputContext(new OutputContextOptions { TypeOutputMode = mode });

        component.WriteOutput(context);
        context.GenerateUsingStatements();

        return context.Output();
    }

    [Theory]
    [InlineData(TypeOutputMode.ShortName, "obj is Task<string>")]
    [InlineData(TypeOutputMode.FullName, "obj is System.Threading.Tasks.Task<string>")]
    [InlineData(TypeOutputMode.Global, "obj is global::System.Threading.Tasks.Task<string>")]
    public void IsKeepsTheWholeType(TypeOutputMode mode, string expected)
    {
        var statement = SyntaxHelpers.Is(
            CodeOutputComponent.Get("obj"),
            TypeDefinition.Get(typeof(Task<string>)));

        Assert.Equal(expected, Write(statement, mode));
    }

    [Fact]
    public void IsKeepsArrayShapesAndContainers()
    {
        Assert.Equal(
            "obj is int[][]",
            Write(SyntaxHelpers.Is(CodeOutputComponent.Get("obj"), TypeDefinition.Get(typeof(int[][]))), TypeOutputMode.ShortName));

        var nested = TypeDefinition.GetNested(TypeDefinition.Get("Ns", "Outer"), "Inner");

        Assert.Equal(
            "obj is Outer.Inner",
            Write(SyntaxHelpers.Is(CodeOutputComponent.Get("obj"), nested), TypeOutputMode.ShortName));

        Assert.Equal(
            "obj is global::Ns.Outer.Inner",
            Write(SyntaxHelpers.Is(CodeOutputComponent.Get("obj"), nested), TypeOutputMode.Global));
    }

    /// <summary>
    /// The using is derived from the type that was written, not asserted by the writer - so
    /// <c>Global</c> mode, which needs no using, gets none.
    /// </summary>
    [Fact]
    public void IsDerivesItsUsingFromTheTypeItWrote()
    {
        var statement = SyntaxHelpers.Is(
            CodeOutputComponent.Get("obj"),
            TypeDefinition.Get(typeof(Task<string>)));

        Assert.Equal(
            "using System.Threading.Tasks;\n\nobj is Task<string>",
            WriteWithUsings(statement, TypeOutputMode.ShortName));

        Assert.Equal(
            "obj is global::System.Threading.Tasks.Task<string>",
            WriteWithUsings(statement, TypeOutputMode.Global));
    }

    [Theory]
    [InlineData(TypeOutputMode.ShortName, "typeof(List<int>[,])")]
    [InlineData(TypeOutputMode.Global, "typeof(global::System.Collections.Generic.List<int>[,])")]
    public void TypeOfKeepsTheWholeType(TypeOutputMode mode, string expected)
    {
        Assert.Equal(expected, Write(SyntaxHelpers.TypeOf(TypeDefinition.Get(typeof(List<int>[,]))), mode));
    }

    [Theory]
    [InlineData(TypeOutputMode.ShortName, "(int[][])value")]
    [InlineData(TypeOutputMode.Global, "(int[][])value")]
    public void StaticCastKeepsTheWholeType(TypeOutputMode mode, string expected)
    {
        Assert.Equal(expected, Write(SyntaxHelpers.StaticCast(TypeDefinition.Get(typeof(int[][])), "value"), mode));
    }
}
