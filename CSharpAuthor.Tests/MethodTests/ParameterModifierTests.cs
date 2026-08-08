using Xunit;

namespace CSharpAuthor.Tests.MethodTests;

public class ParameterModifierTests
{
    [Theory]
    [InlineData(ParameterModifier.None, "public void Test(int value)")]
    [InlineData(ParameterModifier.Ref, "public void Test(ref int value)")]
    [InlineData(ParameterModifier.Out, "public void Test(out int value)")]
    [InlineData(ParameterModifier.In, "public void Test(in int value)")]
    [InlineData(ParameterModifier.RefReadOnly, "public void Test(ref readonly int value)")]
    public void ModifierIsWritten(ParameterModifier modifier, string expected)
    {
        var method = new MethodDefinition("Test");
        method.Modifiers |= ComponentModifier.Public;
        method.AddParameter(typeof(int), "value").Modifier = modifier;

        var context = new OutputContext();
        method.WriteOutput(context);

        Assert.StartsWith(expected, context.Output());
    }

    [Fact]
    public void ParamsIsWritten()
    {
        var method = new MethodDefinition("Test");
        method.Modifiers |= ComponentModifier.Public;
        method.AddParameter(TypeDefinition.Get(typeof(int)).MakeArray(), "values").IsParams = true;

        var context = new OutputContext();
        method.WriteOutput(context);

        Assert.StartsWith("public void Test(params int[] values)", context.Output());
    }

    /// <summary>
    /// An extension method on a struct receiver takes both, which the previous either-or handling
    /// could not express.
    /// </summary>
    [Fact]
    public void ThisCombinesWithAModifier()
    {
        var method = new MethodDefinition("Test");
        method.Modifiers |= ComponentModifier.Public | ComponentModifier.Static;

        var parameter = method.AddParameter(TypeDefinition.Get("Ns", "Point"), "point");
        parameter.This = true;
        parameter.Modifier = ParameterModifier.Ref;

        var context = new OutputContext();
        method.WriteOutput(context);

        Assert.StartsWith("public static void Test(this ref Point point)", context.Output());
    }

    /// <summary>
    /// IsOut predates the modifier and stays the shorthand for it.
    /// </summary>
    [Fact]
    public void IsOutSetsAndReadsTheModifier()
    {
        var parameter = new ParameterDefinition(TypeDefinition.Get(typeof(int)), "value");

        Assert.False(parameter.IsOut);

        parameter.IsOut = true;

        Assert.Equal(ParameterModifier.Out, parameter.Modifier);
        Assert.True(parameter.IsOut);

        parameter.IsOut = false;

        Assert.Equal(ParameterModifier.None, parameter.Modifier);

        parameter.Modifier = ParameterModifier.Out;

        Assert.True(parameter.IsOut);

        parameter.Modifier = ParameterModifier.Ref;

        Assert.False(parameter.IsOut);
    }

    [Fact]
    public void ParametersKeepTheirDefaults()
    {
        var method = new MethodDefinition("Test");
        method.Modifiers |= ComponentModifier.Public;

        var parameter = method.AddParameter(typeof(int), "value");
        parameter.Modifier = ParameterModifier.In;
        parameter.DefaultValue = new CodeOutputComponent("5") { Indented = false };

        var context = new OutputContext();
        method.WriteOutput(context);

        Assert.StartsWith("public void Test(in int value = 5)", context.Output());
    }

    /// <summary>
    /// Forwarding a call repeats ref and out, or it does not compile. in and ref readonly are
    /// optional at a call site and are left off.
    /// </summary>
    [Theory]
    [InlineData(ParameterModifier.None, "value")]
    [InlineData(ParameterModifier.Ref, "ref value")]
    [InlineData(ParameterModifier.Out, "out value")]
    [InlineData(ParameterModifier.In, "value")]
    [InlineData(ParameterModifier.RefReadOnly, "value")]
    public void WrittenAsAnArgument(ParameterModifier modifier, string expected)
    {
        var parameter = new ParameterDefinition(TypeDefinition.Get(typeof(int)), "value")
        {
            Modifier = modifier
        };

        var context = new OutputContext();
        parameter.AsArgument().WriteOutput(context);

        Assert.Equal(expected, context.Output());
    }

    /// <summary>
    /// The whole point of the argument form: a wrapper forwarding to the method it wraps.
    /// </summary>
    [Fact]
    public void ForwardingACallRepeatsTheModifiers()
    {
        var method = new MethodDefinition("TryGet");
        method.Modifiers |= ComponentModifier.Public;
        method.SetReturnType(typeof(bool));

        var key = method.AddParameter(typeof(string), "key");
        var value = method.AddParameter(typeof(int), "value");
        value.Modifier = ParameterModifier.Out;

        method.Return(
            CodeOutputComponent.Get("_inner").Invoke("TryGet", key.AsArgument(), value.AsArgument()));

        var context = new OutputContext();
        method.WriteOutput(context);

        AssertEqual.WithoutNewLine(ForwardingOutput, context.Output());
    }

    private const string ForwardingOutput =
        @"public bool TryGet(string key, out int value)
{
    return _inner.TryGet(
        key,
        out value
    );
}
";

    /// <summary>
    /// A parameter used as an ordinary value expression is still just its name.
    /// </summary>
    [Fact]
    public void WrittenAsAValueIgnoresTheModifier()
    {
        var parameter = new ParameterDefinition(TypeDefinition.Get(typeof(int)), "value")
        {
            Modifier = ParameterModifier.Ref
        };

        var context = new OutputContext();
        parameter.WriteOutput(context);

        Assert.Equal("value", context.Output());
    }
}
