using CSharpAuthor.Expressions;
using Xunit;

namespace CSharpAuthor.Tests.ExpressionTests;

/// <summary>
/// All four lambda shapes — implicit or explicit parameters crossed with an expression or
/// a block body — plus the modifiers and the anonymous-method form they replaced.
/// </summary>
public class LambdaTests
{
    private static ITypeDefinition Int32Type => TypeDefinition.Get(typeof(int));

    private static ITypeDefinition StringType => TypeDefinition.Get(typeof(string));

    [Fact]
    public void FormOneSingleImplicitParameterWithAnExpressionBody()
    {
        ExAssert.Emits("x => x + 1", Ex.Lambda("x", Ex.Add(Ex.Id("x"), Ex.Int(1))));
    }

    [Fact]
    public void FormTwoSeveralImplicitParametersWithAnExpressionBody()
    {
        ExAssert.Emits(
            "(x, y) => x + y",
            Ex.Lambda(new[] { "x", "y" }, Ex.Add(Ex.Id("x"), Ex.Id("y"))));
    }

    [Fact]
    public void FormThreeExplicitlyTypedParametersWithAnExpressionBody()
    {
        var lambda = Ex.Lambda(
            new[] { Ex.Param(Int32Type, "x"), Ex.Param(StringType, "y") },
            Ex.Id("x"));

        ExAssert.Emits("(int x, string y) => x", lambda);
    }

    [Fact]
    public void FormFourABlockBody()
    {
        var lambda = ExLambda.Of("x").Block(
            Ex.Raw("return ", Ex.Id("x")).AsStatement());

        ExAssert.Emits("x =>\n{\n    return x;\n}", lambda);
    }

    [Fact]
    public void ATypedBlockBody()
    {
        var lambda = ExLambda
            .Typed(Ex.Param(Int32Type, "x"))
            .Block(Ex.Raw("return ", Ex.Id("x")).AsStatement());

        ExAssert.Emits("(int x) =>\n{\n    return x;\n}", lambda);
    }

    [Fact]
    public void NoParametersKeepsTheBrackets()
    {
        ExAssert.Emits("() => a", Ex.Lambda(new string[0], Ex.Id("a")));
    }

    [Fact]
    public void ASingleImplicitParameterMayKeepItsBracketsOnRequest()
    {
        ExAssert.Emits("(x) => a", ExLambda.Of("x").Parenthesized().Body(Ex.Id("a")));
    }

    [Fact]
    public void AsyncAndStaticModifiers()
    {
        ExAssert.Emits("async x => a", ExLambda.Of("x").Async().Body(Ex.Id("a")));
        ExAssert.Emits("static x => a", ExLambda.Of("x").Static().Body(Ex.Id("a")));
        ExAssert.Emits("static async x => a", ExLambda.Of("x").Static().Async().Body(Ex.Id("a")));
    }

    [Fact]
    public void AnExplicitReturnTypeForcesTheBracketedParameterForm()
    {
        var lambda = ExLambda.Of("x").Returns(Int32Type).Body(Ex.Id("x"));

        ExAssert.Emits("int (x) => x", lambda);
    }

    [Fact]
    public void ParameterNamesAreKeywordEscaped()
    {
        ExAssert.Emits("@class => a", Ex.Lambda("class", Ex.Id("a")));
    }

    [Fact]
    public void ALambdaInsideACallIsBare()
    {
        var expression = Ex.Id("items").Call("Select", Ex.Lambda("x", Ex.Id("x").Dot("Name")));

        ExAssert.Emits("items.Select(x => x.Name)", expression);
    }

    [Fact]
    public void AnAwaitedLambdaBodyComposes()
    {
        var lambda = ExLambda.Of("x").Async().Body(Ex.Await(Ex.Id("x").Call("Run")));

        ExAssert.Emits("async x => await x.Run()", lambda);
    }

    [Fact]
    public void AnAnonymousMethodWithParameters()
    {
        var expression = Ex.AnonymousMethod(
            new[] { Ex.Param(Int32Type, "x") },
            Ex.Raw("return ", Ex.Id("x")).AsStatement());

        ExAssert.Emits("delegate (int x)\n{\n    return x;\n}", expression);
    }

    [Fact]
    public void AnAnonymousMethodWithNoParameterListMatchesAnySignature()
    {
        var expression = Ex.AnonymousMethod(null, Ex.Raw("return").AsStatement());

        ExAssert.Emits("delegate\n{\n    return;\n}", expression);
    }

    [Fact]
    public void ABlockBodyNestsAtTheSurroundingIndent()
    {
        var lambda = ExLambda.Of("x").Block(
            Ex.Raw("Log(", Ex.Str("hit"), ")").AsStatement(),
            Ex.Raw("return ", Ex.Id("x")).AsStatement());

        var context = new OutputContext();

        context.IncrementIndent();
        lambda.WriteOutput(context);

        Assert.Equal("x =>\n    {\n        Log(\"hit\");\n        return x;\n    }", context.Output());
    }
}
