using Xunit;

namespace CSharpAuthor.Tests.ClassDefinitionTests;

public class GenericClassDefinitionTests
{
    [Fact]
    public void GenericClass()
    {
        var classDefinition = new ClassDefinition("Box");
        classDefinition.AddGenericParameter("T");

        var context = new OutputContext();
        classDefinition.WriteOutput(context);

        AssertEqual.WithoutNewLine(GenericClassOutput, context.Output());
    }

    private const string GenericClassOutput =
        @"public class Box<T>
{
}
";

    [Fact]
    public void GenericClassWithSeveralParameters()
    {
        var classDefinition = new ClassDefinition("Pair");
        classDefinition.AddGenericParameter("TKey");
        classDefinition.AddGenericParameter("TValue");

        var context = new OutputContext();
        classDefinition.WriteOutput(context);

        AssertEqual.WithoutNewLine(SeveralParametersOutput, context.Output());
    }

    private const string SeveralParametersOutput =
        @"public class Pair<TKey, TValue>
{
}
";

    /// <summary>
    /// A generic base type closed over the declaring type's own parameter, which is what a wrapper
    /// deriving from a generic base needs.
    /// </summary>
    [Fact]
    public void GenericClassWithGenericBaseType()
    {
        var classDefinition = new ClassDefinition("Box");
        classDefinition.AddGenericParameter("T");
        classDefinition.AddBaseType(
            new GenericTypeDefinition(
                TypeDefinitionEnum.ClassDefinition,
                "Ns",
                "Container",
                new ITypeDefinition[] { new TypeParameterDefinition("T") }));

        var context = new OutputContext();
        classDefinition.WriteOutput(context);

        AssertEqual.WithoutNewLine(GenericBaseTypeOutput, context.Output());
    }

    private const string GenericBaseTypeOutput =
        @"public class Box<T> : Container<T>
{
}
";

    [Fact]
    public void GenericClassWithConstraints()
    {
        var classDefinition = new ClassDefinition("Box");
        classDefinition.AddGenericParameter("T");
        classDefinition.AddBaseType(TypeDefinition.Get("Ns", "Container"));
        classDefinition.WhereStatement =
            new CodeOutputComponent(" where T : class, new()") { Indented = false };

        var context = new OutputContext();
        classDefinition.WriteOutput(context);

        AssertEqual.WithoutNewLine(ConstraintsOutput, context.Output());
    }

    private const string ConstraintsOutput =
        @"public class Box<T> : Container where T : class, new()
{
}
";

    /// <summary>
    /// The constructor takes the name without the type parameters. Writing them would produce
    /// <c>public Box&lt;T&gt;()</c>, which does not compile.
    /// </summary>
    [Fact]
    public void GenericClassConstructorOmitsTheTypeParameters()
    {
        var classDefinition = new ClassDefinition("Box");
        classDefinition.AddGenericParameter("T");

        var constructor = classDefinition.AddConstructor();
        constructor.AddParameter(new TypeParameterDefinition("T"), "value");
        constructor.AddIndentedStatement("_value = value");

        var context = new OutputContext();
        classDefinition.WriteOutput(context);

        AssertEqual.WithoutNewLine(ConstructorOutput, context.Output());
    }

    private const string ConstructorOutput =
        @"public class Box<T>
{
    public Box(T value)
    {
        _value = value;
    }
}
";

    /// <summary>
    /// The shape a generated wrapper needs: a nested generic class closing a generic base over the
    /// enclosing method's type parameter, repeating its constraints so the call it forwards
    /// satisfies them.
    /// </summary>
    [Fact]
    public void NestedGenericClassWithConstraints()
    {
        var wrapper = new ClassDefinition("Wrapper");

        var state = wrapper.AddClass("State");
        state.Modifiers |= ComponentModifier.Private | ComponentModifier.Sealed;
        state.AddGenericParameter("T");
        state.AddBaseType(
            new GenericTypeDefinition(
                TypeDefinitionEnum.ClassDefinition,
                "Ns",
                "InvocationState",
                new ITypeDefinition[] { new TypeParameterDefinition("T") }));
        state.WhereStatement = new CodeOutputComponent(" where T : class") { Indented = false };

        var invoke = state.AddMethod("Invoke");
        invoke.Modifiers |= ComponentModifier.Public | ComponentModifier.Override;
        invoke.SetReturnType(new TypeParameterDefinition("T"));
        invoke.Return("_inner.Pick<T>(_arg0)");

        var context = new OutputContext();
        wrapper.WriteOutput(context);

        AssertEqual.WithoutNewLine(NestedGenericOutput, context.Output());
    }

    private const string NestedGenericOutput =
        @"public class Wrapper
{
    private sealed class State<T> : InvocationState<T> where T : class
    {
        public override T Invoke()
        {
            return _inner.Pick<T>(_arg0);
        }
    }
}
";
}
