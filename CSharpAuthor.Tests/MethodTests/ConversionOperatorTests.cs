using CSharpAuthor.Expressions;
using CSharpAuthor.Tests.Adversary;
using Xunit;

namespace CSharpAuthor.Tests.MethodTests;

/// <summary>
/// <see cref="ClassDefinition.AddConversionOperator(ConversionOperatorKind,ITypeDefinition,ITypeDefinition,string)"/>.
/// </summary>
/// <remarks>
/// <para>
/// The one gap both preview1004 reviews found independently. There was no facade entry point, so
/// <c>AddMethod("implicit operator int")</c> - the obvious guess - emitted
/// <c>public static void implicit operator int(...)</c>, CS1553.
/// </para>
/// <para>
/// The workaround a reader of the public API arrived at instead was to put the keyword in the
/// return-type slot and the target type in the method name. That emits the right characters, and it
/// is the interesting failure: with the target in a string, the type tracker never sees it, so the
/// output is correct exactly while the target happens to be the enclosing type.
/// <see cref="TheTargetTypeIsTrackedWhenItLivesInAnotherNamespace"/> is that case.
/// </para>
/// </remarks>
public class ConversionOperatorTests
{
    private static readonly ITypeDefinition ShapeType = TypeDefinition.Get("Probe.Shapes", "Shape");
    private static readonly ITypeDefinition CircleType = TypeDefinition.Get("Probe.Primitives", "Circle");

    /// <summary>The declaration Roslyn accepts, in the shape a generator writes it.</summary>
    [Fact]
    public void AnImplicitConversionCompiles()
    {
        var file = new CSharpFileDefinition("Probe.Shapes");

        var shape = file.AddClass("Shape");
        shape.Modifiers = ComponentModifier.Public;
        shape.AddField(typeof(double), "Radius").Modifiers = ComponentModifier.Public;

        var conversion = shape.AddConversionOperator(
            ConversionOperatorKind.Implicit, ShapeType, TypeDefinition.Get(typeof(double)), "radius");

        conversion.Return(Ex.New(ShapeType));

        var code = Emit.File(file, new OutputContextOptions { TypeOutputMode = TypeOutputMode.ShortName });

        Assert.Contains("public static implicit operator Shape(double radius)", code);

        RoslynAssert.Compiles(code);
    }

    /// <summary>The <c>explicit</c> half, and the cast it forces at the call site.</summary>
    [Fact]
    public void AnExplicitConversionCompiles()
    {
        var file = new CSharpFileDefinition("Probe.Shapes");

        var shape = file.AddClass("Shape");
        shape.Modifiers = ComponentModifier.Public;

        var conversion = shape.AddConversionOperator(
            ConversionOperatorKind.Explicit, TypeDefinition.Get(typeof(double)), ShapeType, "shape");

        conversion.Return(Ex.Double(0d));

        var code = Emit.File(file, new OutputContextOptions { TypeOutputMode = TypeOutputMode.ShortName });

        Assert.Contains("public static explicit operator double(Shape shape)", code);

        RoslynAssert.Compiles(code);
    }

    /// <summary>
    /// The target type is tracked, so it is qualified and imported like any other type.
    /// </summary>
    /// <remarks>
    /// This is the whole reason the typed entry point exists. The string route emitted a bare
    /// <c>Shape</c> here with nothing importing it - CS0246 - and did so silently, because a name
    /// in a string is not a type as far as anything downstream is concerned.
    /// </remarks>
    [Fact]
    public void TheTargetTypeIsTrackedWhenItLivesInAnotherNamespace()
    {
        var code = ConvertingCircleToShape(
            new OutputContextOptions { TypeOutputMode = TypeOutputMode.ShortName });

        // Derived, not written by the caller: nothing above says AddImportNamespace.
        Assert.Contains("using Probe.Shapes;", code);
        Assert.Contains("public static implicit operator Shape(Circle value)", code);
    }

    /// <summary>And the same tree, with one option flipped, qualifies instead of importing.</summary>
    [Fact]
    public void TheTargetTypeFollowsTheFilesTypeOutputMode()
    {
        var code = ConvertingCircleToShape(
            new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        Assert.Contains(
            "public static implicit operator global::Probe.Shapes.Shape(global::Probe.Primitives.Circle value)",
            code);

        Assert.DoesNotContain("using Probe.Shapes;", code);
    }

    /// <summary>
    /// <c>public static</c> is set for you, because C# allows a conversion operator nothing else.
    /// </summary>
    [Fact]
    public void PublicAndStaticAreSetWithoutBeingAsked()
    {
        var definition = new ConversionOperatorDefinition(ConversionOperatorKind.Implicit, ShapeType);

        Assert.Equal(
            ComponentModifier.Public | ComponentModifier.Static,
            definition.Modifiers);
    }

    private static string ConvertingCircleToShape(OutputContextOptions options)
    {
        var file = new CSharpFileDefinition("Probe.Primitives");

        var circle = file.AddClass("Circle");
        circle.Modifiers = ComponentModifier.Public;

        var conversion = circle.AddConversionOperator(
            ConversionOperatorKind.Implicit, ShapeType, CircleType, "value");

        conversion.Return(Ex.Null);

        return Emit.File(file, options);
    }
}
