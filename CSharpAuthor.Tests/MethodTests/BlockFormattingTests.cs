using System.Collections.Generic;
using Xunit;

namespace CSharpAuthor.Tests.MethodTests;

public class BlockFormattingTests
{
    [Fact]
    public void WhileKeepsTheSpaceBeforeItsCondition()
    {
        var method = new MethodDefinition("Test");

        method.While(CodeOutputComponent.Get("keepGoing")).AddCode("i++;");

        AssertEqual.ContainsWithoutNewLine("    while (keepGoing)\n", Write(method));
    }

    /// <summary>
    /// The enclosing <c>while (</c> already writes the parentheses, the same as <c>if</c> does.
    /// </summary>
    [Fact]
    public void WhileDoesNotDoubleTheParenthesesOfALogicStatement()
    {
        var method = new MethodDefinition("Test");

        method.While(
            SyntaxHelpers.And(CodeOutputComponent.Get("a"), CodeOutputComponent.Get("b")))
            .AddCode("i++;");

        AssertEqual.ContainsWithoutNewLine("    while (a && b)\n", Write(method));
    }

    /// <summary>
    /// The loop variable's keyword used to be part of the literal <c>"foreach(var "</c>, so an
    /// explicitly typed loop could not be expressed at all.
    /// </summary>
    [Fact]
    public void ForEachCanDeclareAnExplicitType()
    {
        var method = new MethodDefinition("Test");

        var items = method.AddParameter(typeof(IEnumerable<object>), "items");

        method.ForEach(TypeDefinition.Get("Sample.Models", "Widget"), "item", items)
            .AddCode("Use(item);");

        var context = new OutputContext();

        method.WriteOutput(context);
        context.GenerateUsingStatements();

        var output = context.Output();

        AssertEqual.ContainsWithoutNewLine("    foreach (Widget item in items)\n", output);

        // Passing the type also lets it reach import derivation.
        AssertEqual.ContainsWithoutNewLine("using Sample.Models;", output);
    }

    [Fact]
    public void ForEachStillDefaultsToVar()
    {
        var method = new MethodDefinition("Test");

        var items = method.AddParameter(typeof(IEnumerable<object>), "items");

        method.ForEach("item", items).AddCode("Use(item);");

        AssertEqual.ContainsWithoutNewLine("    foreach (var item in items)\n", Write(method));
    }

    [Fact]
    public void ObjectInitializerIsSpacedFromTheConstructorCall()
    {
        var method = new MethodDefinition("Test");

        var statement = SyntaxHelpers.New(TypeDefinition.Get("Sample", "Profile"));

        statement.AddInitValue(CodeOutputComponent.Get("Handle = h"));

        method.Assign(statement).ToVar("profile");

        AssertEqual.ContainsWithoutNewLine(
            "    var profile = new Profile() { Handle = h };\n", Write(method));
    }

    private static string Write(IOutputComponent component)
    {
        var context = new OutputContext();

        component.WriteOutput(context);

        return context.Output();
    }
}
