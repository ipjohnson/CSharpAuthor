using Xunit;

namespace CSharpAuthor.Tests.StatementTests;

/// <summary>
/// <c>for</c> loops, which the library declared a class for and then never wrote.
/// </summary>
/// <remarks>
/// <see cref="ForDefinition"/> had an empty <c>WriteComponentOutput</c> and nothing returned one,
/// so constructing it produced a loop that wrote nothing - body included.
/// </remarks>
public class ForDefinitionTests
{
    [Fact]
    public void CountingLoop()
    {
        var method = new MethodDefinition("Method") { Modifiers = ComponentModifier.Public };

        var forLoop = method.For("i", 0, 10);

        forLoop.AddCode("Console.WriteLine(i);");

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine(
            "    for(var i = 0; i < 10; i++)\n    {\n        Console.WriteLine(i);\n    }\n",
            outputContext.Output());
    }

    [Fact]
    public void TheLoopVariableIsUsableAsAValue()
    {
        var method = new MethodDefinition("Method") { Modifiers = ComponentModifier.Public };

        var forLoop = method.For("index", 0, 5);

        forLoop.Add(SyntaxHelpers.Invoke("Use", forLoop.Variable!));

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        var output = outputContext.Output();

        AssertEqual.ContainsWithoutNewLine("for(var index = 0; index < 5; index++)", output);
        AssertEqual.ContainsWithoutNewLine("Use(index)", output);
    }

    [Fact]
    public void TheLimitCanBeAnExpression()
    {
        var method = new MethodDefinition("Method") { Modifiers = ComponentModifier.Public };

        method.For("i", 0, "items.Count");

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine(
            "for(var i = 0; i < items.Count; i++)", outputContext.Output());
    }

    [Fact]
    public void AllThreeClausesGivenDirectly()
    {
        var method = new MethodDefinition("Method") { Modifiers = ComponentModifier.Public };

        method.For(
            CodeOutputComponent.Get("var i = 10"),
            CodeOutputComponent.Get("i > 0"),
            CodeOutputComponent.Get("i--"));

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine("for(var i = 10; i > 0; i--)", outputContext.Output());
    }

    [Fact]
    public void EveryClauseMayBeOmitted()
    {
        // for(;;) is a legal infinite loop, and each clause is independently optional.
        var method = new MethodDefinition("Method") { Modifiers = ComponentModifier.Public };

        var forLoop = method.For(null, null, null);

        forLoop.Break();

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine("for(; ; )\n", outputContext.Output());
    }

    [Fact]
    public void ALoopVariableNamedAfterAKeywordIsEscaped()
    {
        var method = new MethodDefinition("Method") { Modifiers = ComponentModifier.Public };

        method.For("event", 0, 3);

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine(
            "for(var @event = 0; @event < 3; @event++)", outputContext.Output());
    }

    [Fact]
    public void NestedLoopsIndentTheirBodies()
    {
        var method = new MethodDefinition("Method") { Modifiers = ComponentModifier.Public };

        var outer = method.For("i", 0, 2);
        var inner = outer.For("j", 0, 2);

        inner.AddCode("Use(i, j);");

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine(
            "    for(var i = 0; i < 2; i++)\n" +
            "    {\n" +
            "        for(var j = 0; j < 2; j++)\n" +
            "        {\n" +
            "            Use(i, j);\n" +
            "        }\n" +
            "    }\n",
            outputContext.Output());
    }

    [Fact]
    public void ContinueSkipsAnIteration()
    {
        var method = new MethodDefinition("Method") { Modifiers = ComponentModifier.Public };

        var forLoop = method.For("i", 0, 10);

        forLoop.Continue();

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        // There was no Continue() at all, only Break().
        AssertEqual.ContainsWithoutNewLine("        continue;\n", outputContext.Output());
    }

    [Fact]
    public void ContinueAndBreakAreBothAvailableOnAnyBlock()
    {
        var whileLoop = new WhileDefinition(CodeOutputComponent.Get("true"));

        whileLoop.Continue();
        whileLoop.Break();

        var outputContext = new OutputContext();

        whileLoop.WriteOutput(outputContext);

        var output = outputContext.Output();

        AssertEqual.ContainsWithoutNewLine("continue;", output);
        AssertEqual.ContainsWithoutNewLine("break;", output);
    }
}
