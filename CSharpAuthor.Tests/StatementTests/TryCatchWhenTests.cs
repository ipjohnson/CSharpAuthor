using System;
using Xunit;

namespace CSharpAuthor.Tests.StatementTests;

/// <summary>
/// <c>catch ... when</c>, which one of the two overloads accepted and then discarded.
/// </summary>
/// <remarks>
/// A filter is the difference between handling an exception and swallowing it. Dropped, the clause
/// catches everything of that type, so an exception meant for an outer handler is caught here
/// instead - and the stack is already unwound by the time anything could notice.
/// </remarks>
public class TryCatchWhenTests
{
    [Fact]
    public void TheTypeOverloadForwardsItsFilter()
    {
        var method = new MethodDefinition("Method") { Modifiers = ComponentModifier.Public };

        var tryBlock = method.Try();

        tryBlock.AddCode("Risky();");

        tryBlock.Catch(
            typeof(InvalidOperationException),
            "exception",
            CodeOutputComponent.Get("exception.Message != null"));

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine(
            "catch (InvalidOperationException exception) when (exception.Message != null)",
            outputContext.Output());
    }

    [Fact]
    public void TheTypeDefinitionOverloadForwardsItsFilterToo()
    {
        var method = new MethodDefinition("Method") { Modifiers = ComponentModifier.Public };

        var tryBlock = method.Try();

        tryBlock.Catch(
            TypeDefinition.Get(typeof(InvalidOperationException)),
            "exception",
            CodeOutputComponent.Get("exception.Message != null"));

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine(
            "catch (InvalidOperationException exception) when (exception.Message != null)",
            outputContext.Output());
    }

    [Fact]
    public void ACatchWithNoFilterWritesNoWhenClause()
    {
        var method = new MethodDefinition("Method") { Modifiers = ComponentModifier.Public };

        var tryBlock = method.Try();

        tryBlock.Catch(typeof(Exception), "exception");

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        var output = outputContext.Output();

        AssertEqual.ContainsWithoutNewLine("catch (Exception exception)", output);
        Assert.DoesNotContain("when", output);
    }

    [Fact]
    public void SeveralCatchesEachKeepTheirOwnFilter()
    {
        var method = new MethodDefinition("Method") { Modifiers = ComponentModifier.Public };

        var tryBlock = method.Try();

        tryBlock.Catch(
            typeof(ArgumentException), "first", CodeOutputComponent.Get("first.ParamName != null"));

        tryBlock.Catch(
            typeof(InvalidOperationException), "second", CodeOutputComponent.Get("Retry()"));

        tryBlock.Finally().AddCode("Cleanup();");

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        var output = outputContext.Output();

        AssertEqual.ContainsWithoutNewLine(
            "catch (ArgumentException first) when (first.ParamName != null)", output);
        AssertEqual.ContainsWithoutNewLine(
            "catch (InvalidOperationException second) when (Retry())", output);
        AssertEqual.ContainsWithoutNewLine("finally", output);
    }

    [Fact]
    public void AFilterWithNoExceptionName()
    {
        var method = new MethodDefinition("Method") { Modifiers = ComponentModifier.Public };

        var tryBlock = method.Try();

        tryBlock.Catch(typeof(Exception), when: CodeOutputComponent.Get("ShouldHandle()"));

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine(
            "catch (Exception) when (ShouldHandle())", outputContext.Output());
    }
}
