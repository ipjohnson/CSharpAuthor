using Xunit;

namespace CSharpAuthor.Tests.MethodTests;

public class MethodConstraintTests
{
    [Fact]
    public void GenericMethodWithConstraint()
    {
        var method = new MethodDefinition("Pick");
        method.Modifiers |= ComponentModifier.Public;
        method.AddGenericParameter(new TypeParameterDefinition("T"));
        method.SetReturnType(new TypeParameterDefinition("T"));
        method.AddParameter(new TypeParameterDefinition("T"), "item");
        method.AddConstraint("T").Class();
        method.Return("item");

        AssertEqual.WithoutNewLine(
            @"public T Pick<T>(T item) where T : class
{
    return item;
}
",
            Write(method));
    }

    /// <summary>
    /// The shape a forwarding wrapper needs: the constraints of the member it forwards to, repeated
    /// so the call satisfies them.
    /// </summary>
    [Fact]
    public void SeveralParametersEachGetTheirOwnClause()
    {
        var method = new MethodDefinition("Convert");
        method.Modifiers |= ComponentModifier.Public;
        method.AddGenericParameter(new TypeParameterDefinition("TIn"));
        method.AddGenericParameter(new TypeParameterDefinition("TOut"));
        method.SetReturnType(new TypeParameterDefinition("TOut"));
        method.AddParameter(new TypeParameterDefinition("TIn"), "value");
        method.AddConstraint("TIn").Implements(TypeDefinition.Get("Ns", "IThing"));
        method.AddConstraint("TOut").Class().DefaultConstructor();
        method.Return("default(TOut)");

        Assert.Contains(
            "public TOut Convert<TIn, TOut>(TIn value) where TIn : IThing where TOut : class, new()",
            Write(method));
    }

    private static string Write(MethodDefinition method)
    {
        var context = new OutputContext();

        method.WriteOutput(context);

        return context.Output();
    }
}
