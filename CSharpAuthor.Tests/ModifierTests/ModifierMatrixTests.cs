using Xunit;

namespace CSharpAuthor.Tests.ModifierTests;

/// <summary>
/// Modifier combinations, on members and on types.
/// </summary>
/// <remarks>
/// Every writer chose its modifier with a chain of <c>else if</c>, so exactly one could ever be
/// written and every other one asked for was dropped without complaint. The combinations that
/// suffered are the ones C# actually requires: <c>sealed</c> is only legal on a member together
/// with <c>override</c>, and that pair lost its <c>sealed</c>. <c>partial</c> and <c>readonly</c>
/// were not written at all.
/// </remarks>
public class ModifierMatrixTests
{
    private static string WriteMethod(ComponentModifier modifiers)
    {
        var method = new MethodDefinition("Method")
        {
            Modifiers = modifiers | ComponentModifier.Public
        };

        method.SetReturnType(TypeDefinition.Get(typeof(void)));

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        return outputContext.Output();
    }

    private static string WriteClass(ComponentModifier modifiers, ClassKeyword keyword)
    {
        var classDefinition = new ClassDefinition("Widget")
        {
            Modifiers = modifiers | ComponentModifier.Public,
            TypeKeyword = keyword
        };

        var outputContext = new OutputContext();

        classDefinition.WriteOutput(outputContext);

        return outputContext.Output();
    }

    [Theory]
    // The single modifiers, which already worked, so that the matrix proves it stayed that way.
    [InlineData(ComponentModifier.Static, "public static void Method()")]
    [InlineData(ComponentModifier.Virtual, "public virtual void Method()")]
    [InlineData(ComponentModifier.Override, "public override void Method()")]
    [InlineData(ComponentModifier.Async, "public async void Method()")]
    // Never written before.
    [InlineData(ComponentModifier.Partial, "public partial void Method()")]
    [InlineData(ComponentModifier.Readonly, "public readonly void Method()")]
    // Combinations, where the chain used to keep one and drop the rest.
    [InlineData(
        ComponentModifier.Sealed | ComponentModifier.Override,
        "public sealed override void Method()")]
    [InlineData(
        ComponentModifier.Static | ComponentModifier.Async,
        "public static async void Method()")]
    [InlineData(
        ComponentModifier.Static | ComponentModifier.Partial,
        "public static partial void Method()")]
    [InlineData(
        ComponentModifier.Readonly | ComponentModifier.Override,
        "public override readonly void Method()")]
    [InlineData(
        ComponentModifier.Virtual | ComponentModifier.Async,
        "public virtual async void Method()")]
    public void MethodModifierCombinations(ComponentModifier modifiers, string expected)
    {
        AssertEqual.ContainsWithoutNewLine(expected, WriteMethod(modifiers));
    }

    [Fact]
    public void AbstractMethodDeclaresNoBody()
    {
        var output = WriteMethod(ComponentModifier.Abstract);

        // CS0500 before this: the modifier was dropped and a `{ }` body written in its place, so
        // the method compiled as an ordinary empty one.
        AssertEqual.ContainsWithoutNewLine("public abstract void Method();", output);
        Assert.DoesNotContain("{", output);
        Assert.DoesNotContain("}", output);
    }

    [Fact]
    public void AbstractSurvivesAlongsideStatic()
    {
        // The shape a C# 11 static abstract interface member takes.
        AssertEqual.ContainsWithoutNewLine(
            "public static abstract void Method();",
            WriteMethod(ComponentModifier.Static | ComponentModifier.Abstract));
    }

    [Fact]
    public void AbstractSurvivesAlongsideSealed()
    {
        // Not legal C# on a member, and written anyway - the compiler rejecting a combination is
        // better than this library dropping half of it. The library does not validate what it is
        // handed anywhere else either.
        AssertEqual.ContainsWithoutNewLine(
            "public abstract sealed void Method();",
            WriteMethod(ComponentModifier.Abstract | ComponentModifier.Sealed));
    }

    [Fact]
    public void AbstractMethodWithStatementsStillDeclaresNoBody()
    {
        var method = new MethodDefinition("Method")
        {
            Modifiers = ComponentModifier.Public | ComponentModifier.Abstract
        };

        method.SetReturnType(TypeDefinition.Get(typeof(void)));
        method.AddCode("DoSomething();");

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine("public abstract void Method();", outputContext.Output());
        Assert.DoesNotContain("DoSomething", outputContext.Output());
    }

    [Fact]
    public void OmitBodyDeclaresThePartialHalfWithNoBody()
    {
        var method = new MethodDefinition("Method")
        {
            Modifiers = ComponentModifier.Public | ComponentModifier.Partial,
            OmitBody = true
        };

        method.SetReturnType(TypeDefinition.Get(typeof(void)));

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine("public partial void Method();", outputContext.Output());
    }

    [Fact]
    public void APartialMethodStillWritesItsBodyByDefault()
    {
        // The implementing half. Only OmitBody or abstract removes a body, so marking a method
        // partial does not silently change what it already emitted.
        var output = WriteMethod(ComponentModifier.Partial);

        AssertEqual.ContainsWithoutNewLine("public partial void Method()", output);
        Assert.Contains("{", output);
    }

    [Fact]
    public void AbstractMethodKeepsItsConstraints()
    {
        var method = new MethodDefinition("Method")
        {
            Modifiers = ComponentModifier.Public | ComponentModifier.Abstract
        };

        method.SetReturnType(TypeDefinition.Get(typeof(void)));
        method.AddGenericParameter(new TypeParameterDefinition("T"));
        method.AddConstraint("T").Class();

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine(
            "public abstract void Method<T>() where T : class;", outputContext.Output());
    }

    [Theory]
    [InlineData(ComponentModifier.Sealed, ClassKeyword.Class, "public sealed class Widget")]
    [InlineData(ComponentModifier.Static, ClassKeyword.Class, "public static class Widget")]
    [InlineData(ComponentModifier.Abstract, ClassKeyword.Class, "public abstract class Widget")]
    [InlineData(
        ComponentModifier.Sealed | ComponentModifier.Partial,
        ClassKeyword.Class,
        "public sealed partial class Widget")]
    [InlineData(
        ComponentModifier.Abstract | ComponentModifier.Partial,
        ClassKeyword.Class,
        "public abstract partial class Widget")]
    [InlineData(
        ComponentModifier.Static | ComponentModifier.Partial,
        ClassKeyword.Class,
        "public static partial class Widget")]
    public void TypeModifierCombinations(
        ComponentModifier modifiers, ClassKeyword keyword, string expected)
    {
        AssertEqual.ContainsWithoutNewLine(expected, WriteClass(modifiers, keyword));
    }

    [Fact]
    public void ReadonlyStructKeepsItsReadonly()
    {
        // Dropped entirely before this, so a type declared immutable came out mutable.
        AssertEqual.ContainsWithoutNewLine(
            "public readonly struct Widget",
            WriteClass(ComponentModifier.Readonly, ClassKeyword.Struct));
    }

    [Fact]
    public void ReadonlyRecordStructKeepsBothWords()
    {
        AssertEqual.ContainsWithoutNewLine(
            "public readonly record struct Widget",
            WriteClass(ComponentModifier.Readonly, ClassKeyword.RecordStruct));
    }

    [Fact]
    public void ReadonlyPartialStruct()
    {
        AssertEqual.ContainsWithoutNewLine(
            "public readonly partial struct Widget",
            WriteClass(
                ComponentModifier.Readonly | ComponentModifier.Partial, ClassKeyword.Struct));
    }

    [Fact]
    public void StaticReadonlyFieldIsWrittenInThatOrder()
    {
        var field = new FieldDefinition(TypeDefinition.Get(typeof(int)), "_count")
        {
            Modifiers = ComponentModifier.Private |
                        ComponentModifier.Static |
                        ComponentModifier.Readonly
        };

        var outputContext = new OutputContext();

        field.WriteOutput(outputContext);

        // Written as `readonly static` before, which compiles and reads like a mistake.
        AssertEqual.ContainsWithoutNewLine("private static readonly ", outputContext.Output());
    }

    [Fact]
    public void PropertyKeepsSealedAlongsideOverride()
    {
        var property = new PropertyDefinition(TypeDefinition.Get(typeof(int)), "Count")
        {
            Modifiers = ComponentModifier.Public |
                        ComponentModifier.Sealed |
                        ComponentModifier.Override
        };

        var outputContext = new OutputContext();

        property.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine("public sealed override ", outputContext.Output());
    }

    [Fact]
    public void PropertyKeepsStaticAlongsideVirtual()
    {
        var property = new PropertyDefinition(TypeDefinition.Get(typeof(int)), "Count")
        {
            Modifiers = ComponentModifier.Public |
                        ComponentModifier.Static |
                        ComponentModifier.Virtual
        };

        var outputContext = new OutputContext();

        property.WriteOutput(outputContext);

        // `static` used to be written only when there was no virtual or override, so this pair
        // came out as `virtual` alone.
        AssertEqual.ContainsWithoutNewLine("public static virtual ", outputContext.Output());
    }

    [Fact]
    public void EventKeepsSealedAlongsideOverride()
    {
        var eventDefinition = new EventDefinition(
            TypeDefinition.Get(typeof(System.EventHandler)), "Changed")
        {
            Modifiers = ComponentModifier.Public |
                        ComponentModifier.Sealed |
                        ComponentModifier.Override
        };

        var outputContext = new OutputContext();

        eventDefinition.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine("public sealed override event ", outputContext.Output());
    }

    [Fact]
    public void ExplicitInterfaceImplementationTakesNoAccessibilityButKeepsAsync()
    {
        var method = new MethodDefinition("Method")
        {
            Modifiers = ComponentModifier.Public | ComponentModifier.Async,
            InterfaceImplementation = TypeDefinition.Get(typeof(System.IDisposable))
        };

        method.SetReturnType(TypeDefinition.Get(typeof(void)));

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        var output = outputContext.Output();

        Assert.DoesNotContain("public", output);
        AssertEqual.ContainsWithoutNewLine("async void IDisposable.Method()", output);
    }
}
