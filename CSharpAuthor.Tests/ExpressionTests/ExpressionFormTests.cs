using CSharpAuthor.Expressions;
using Xunit;

namespace CSharpAuthor.Tests.ExpressionTests;

/// <summary>
/// The rest of the expression surface: object and collection creation, tuples and
/// deconstruction, ranges and indices, <c>with</c>, <c>stackalloc</c>, and the argument
/// modifiers.
/// </summary>
public class ExpressionFormTests
{
    private static ITypeDefinition Widget => TypeDefinition.Get("TestNamespace", "Widget");

    private static ITypeDefinition Int32Type => TypeDefinition.Get(typeof(int));

    private static ITypeDefinition StringType => TypeDefinition.Get(typeof(string));

    // -------------------------------------------------------------------------------
    // Member access and invocation
    // -------------------------------------------------------------------------------

    [Fact]
    public void MemberChainsAndCalls()
    {
        ExAssert.Emits("a.b.c", Ex.Id("a").Dot("b").Dot("c"));
        ExAssert.Emits("a.b(c, d)", Ex.Id("a").Call("b", Ex.Id("c"), Ex.Id("d")));
        ExAssert.Emits("a[0]", Ex.Id("a").Index(Ex.Int(0)));
        ExAssert.Emits("a[i, j]", Ex.Id("a").Index(Ex.Id("i"), Ex.Id("j")));
        ExAssert.Emits("a++", Ex.Id("a").PlusPlus());
        ExAssert.Emits("a--", Ex.Id("a").MinusMinus());
    }

    [Fact]
    public void StaticMemberAccessKeepsTheTypeDeferred()
    {
        var context = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        Ex.On(Widget, "Default").WriteOutput(context);

        Assert.Equal("global::TestNamespace.Widget.Default", context.Output());
    }

    [Fact]
    public void GenericCalls()
    {
        ExAssert.Emits(
            "a.Cast<int>(b)",
            Ex.Id("a").CallGeneric("Cast", new[] { Int32Type }, Ex.Id("b")));

        ExAssert.Emits(
            "Widget.Create<int, string>()",
            Ex.CallGeneric(Widget, "Create", new[] { Int32Type, StringType }));
    }

    // -------------------------------------------------------------------------------
    // Object creation
    // -------------------------------------------------------------------------------

    [Fact]
    public void ObjectCreationForms()
    {
        ExAssert.Emits("new Widget()", Ex.New(Widget));
        ExAssert.Emits("new Widget(a, b)", Ex.New(Widget, Ex.Id("a"), Ex.Id("b")));
        ExAssert.Emits("new(a)", Ex.NewTargetTyped(Ex.Id("a")));
        ExAssert.Emits("new()", Ex.NewTargetTyped());
    }

    [Fact]
    public void ObjectInitializers()
    {
        ExAssert.Emits(
            "new Widget { X = 1, Y = 2 }",
            Ex.NewWithInitializer(
                Widget,
                null,
                Ex.Assign(Ex.Id("X"), Ex.Int(1)),
                Ex.Assign(Ex.Id("Y"), Ex.Int(2))));

        ExAssert.Emits(
            "new Widget() { X = 1 }",
            Ex.NewWithInitializer(Widget, new Ex[0], Ex.Assign(Ex.Id("X"), Ex.Int(1))));
    }

    [Fact]
    public void AnObjectInitializerIsStillPrimaryAndComposes()
    {
        // Verified to compile: `new Node { V = 1 }.M()`.
        var expression = Ex
            .NewWithInitializer(Widget, null, Ex.Assign(Ex.Id("X"), Ex.Int(1)))
            .Call("Build");

        ExAssert.Emits("new Widget { X = 1 }.Build()", expression);
    }

    [Fact]
    public void AnonymousObjects()
    {
        ExAssert.Emits(
            "new { X = 1, Y = a }",
            Ex.NewAnonymous(Ex.Assign(Ex.Id("X"), Ex.Int(1)), Ex.Assign(Ex.Id("Y"), Ex.Id("a"))));
    }

    [Fact]
    public void ArrayCreationForms()
    {
        ExAssert.Emits("new int[] { 1, 2 }", Ex.NewArray(Int32Type, Ex.Int(1), Ex.Int(2)));
        ExAssert.Emits("new int[] { }", Ex.NewArray(Int32Type));
        ExAssert.Emits("new int[10]", Ex.NewArraySized(Int32Type, Ex.Int(10)));
        ExAssert.Emits("new int[n, m]", Ex.NewArraySized(Int32Type, Ex.Id("n"), Ex.Id("m")));
        ExAssert.Emits("new[] { 1, 2 }", Ex.NewArrayImplicit(Ex.Int(1), Ex.Int(2)));
    }

    [Fact]
    public void GenericObjectCreationWithSeparateTypeArguments()
    {
        ExAssert.Emits(
            "new Widget<int>()",
            Ex.NewGeneric(Widget, new[] { Int32Type }));
    }

    // -------------------------------------------------------------------------------
    // Collection expressions
    // -------------------------------------------------------------------------------

    [Fact]
    public void CollectionExpressionsWithAndWithoutSpreads()
    {
        ExAssert.Emits("[]", Ex.Collection());
        ExAssert.Emits("[a, b]", Ex.Collection(Ex.Id("a"), Ex.Id("b")));
        ExAssert.Emits("[a, ..rest]", Ex.Collection(Ex.Id("a"), Ex.Spread(Ex.Id("rest"))));
        ExAssert.Emits("[..a, ..b]", Ex.Collection(Ex.Spread(Ex.Id("a")), Ex.Spread(Ex.Id("b"))));
    }

    [Fact]
    public void ASpreadOfALooserExpressionIsBracketed()
    {
        // The spread operand is a unary expression, the same requirement as a range end.
        ExAssert.Emits("[..(a ?? b)]", Ex.Collection(Ex.Spread(Ex.Coalesce(Ex.Id("a"), Ex.Id("b")))));
    }

    [Fact]
    public void ACollectionExpressionNestsInsideAnother()
    {
        ExAssert.Emits(
            "[[a], [b]]",
            Ex.Collection(Ex.Collection(Ex.Id("a")), Ex.Collection(Ex.Id("b"))));
    }

    // -------------------------------------------------------------------------------
    // Tuples and deconstruction
    // -------------------------------------------------------------------------------

    [Fact]
    public void Tuples()
    {
        ExAssert.Emits("(a, b)", Ex.Tuple(Ex.Id("a"), Ex.Id("b")));
        ExAssert.Emits(
            "(x: a, y: b)",
            Ex.Tuple(Ex.Named("x", Ex.Id("a")), Ex.Named("y", Ex.Id("b"))));
    }

    [Fact]
    public void ATupleElementMayBeALooseExpression()
    {
        ExAssert.Emits("(a + b, c ?? d)",
            Ex.Tuple(Ex.Add(Ex.Id("a"), Ex.Id("b")), Ex.Coalesce(Ex.Id("c"), Ex.Id("d"))));
    }

    [Fact]
    public void DeconstructionAssignments()
    {
        ExAssert.Emits(
            "var (a, b) = source",
            Ex.Assign(Ex.VarTuple("a", "b"), Ex.Id("source")));

        ExAssert.Emits(
            "(a, b) = source",
            Ex.Assign(Ex.Tuple(Ex.Id("a"), Ex.Id("b")), Ex.Id("source")));

        ExAssert.Emits(
            "(int a, string b) = source",
            Ex.Assign(
                Ex.TypedTuple(Ex.Param(Int32Type, "a"), Ex.Param(StringType, "b")),
                Ex.Id("source")));
    }

    [Fact]
    public void ADeconstructionMayDiscard()
    {
        ExAssert.Emits("(a, _) = source", Ex.Assign(Ex.Tuple(Ex.Id("a"), Ex.Discard), Ex.Id("source")));
    }

    // -------------------------------------------------------------------------------
    // with
    // -------------------------------------------------------------------------------

    [Fact]
    public void WithExpressions()
    {
        ExAssert.Emits(
            "record with { X = 1 }",
            Ex.With(Ex.Id("record"), Ex.Assign(Ex.Id("X"), Ex.Int(1))));

        ExAssert.Emits("record with { }", Ex.With(Ex.Id("record")));
    }

    // -------------------------------------------------------------------------------
    // stackalloc
    // -------------------------------------------------------------------------------

    [Fact]
    public void StackAllocInExpressionPosition()
    {
        ExAssert.Emits("stackalloc int[10]", Ex.StackAlloc(Int32Type, Ex.Int(10)));
        ExAssert.Emits("stackalloc int[] { 1, 2 }", Ex.StackAllocInit(Int32Type, Ex.Int(1), Ex.Int(2)));
        ExAssert.Emits("stackalloc[] { 1, 2 }", Ex.StackAllocImplicit(Ex.Int(1), Ex.Int(2)));
    }

    [Fact]
    public void StackAllocAssignedToASpan()
    {
        var expression = Ex.Assign(Ex.Id("buffer"), Ex.StackAlloc(Int32Type, Ex.Id("size")));

        ExAssert.Emits("buffer = stackalloc int[size]", expression);
    }

    // -------------------------------------------------------------------------------
    // Argument modifiers
    // -------------------------------------------------------------------------------

    [Fact]
    public void ArgumentModifiersAreNeverBracketed()
    {
        var expression = Ex.Id("f").Invoke(
            Ex.Named("count", Ex.Int(1)),
            Ex.OutArg(Ex.Id("existing")),
            Ex.OutVar(Widget, "made"),
            Ex.OutVar("inferred"),
            Ex.OutDiscard(),
            Ex.RefArg(Ex.Id("slot")),
            Ex.InArg(Ex.Id("readOnly")));

        ExAssert.Emits(
            "f(count: 1, out existing, out Widget made, out var inferred, out _, ref slot, in readOnly)",
            expression);
    }

    // -------------------------------------------------------------------------------
    // Keyword primaries
    // -------------------------------------------------------------------------------

    [Fact]
    public void KeywordPrimaries()
    {
        ExAssert.Emits("typeof(Widget)", Ex.TypeOf(Widget));
        ExAssert.Emits("nameof(a.b)", Ex.NameOf(Ex.Id("a").Dot("b")));
        ExAssert.Emits("sizeof(int)", Ex.SizeOf(Int32Type));
        ExAssert.Emits("default", Ex.Default());
        ExAssert.Emits("default(int)", Ex.Default(Int32Type));
        ExAssert.Emits("checked(a + b)", Ex.Checked(Ex.Add(Ex.Id("a"), Ex.Id("b"))));
        ExAssert.Emits("unchecked(a * b)", Ex.Unchecked(Ex.Multiply(Ex.Id("a"), Ex.Id("b"))));
        ExAssert.Emits("this.a", Ex.This.Dot("a"));
        ExAssert.Emits("base.a()", Ex.Base.Call("a"));
    }

    [Fact]
    public void TypeOfKeepsTheTypeDeferredAcrossModes()
    {
        var shortName = new OutputContext();
        var global = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        Ex.TypeOf(Widget).WriteOutput(shortName);
        Ex.TypeOf(Widget).WriteOutput(global);

        shortName.GenerateUsingStatements();
        global.GenerateUsingStatements();

        Assert.Equal("using TestNamespace;\n\ntypeof(Widget)", shortName.Output());
        Assert.Equal("typeof(global::TestNamespace.Widget)", global.Output());
    }

    // -------------------------------------------------------------------------------
    // A composite, to show the pieces meet
    // -------------------------------------------------------------------------------

    [Fact]
    public void ARealisticRegistrationLine()
    {
        var expression = Ex
            .Id("services")
            .Call(
                "Add",
                Ex.New(
                    Widget,
                    Ex.TypeOf(TypeDefinition.Get("TestNamespace", "IService")),
                    Ex.Lambda("provider", Ex.New(TypeDefinition.Get("TestNamespace", "Service"))),
                    Ex.On(TypeDefinition.Get("TestNamespace", "ServiceLifetime"), "Singleton")));

        var context = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        expression.WriteOutput(context);
        context.GenerateUsingStatements();

        Assert.Equal(
            "services.Add(new global::TestNamespace.Widget(typeof(global::TestNamespace.IService), " +
            "provider => new global::TestNamespace.Service(), " +
            "global::TestNamespace.ServiceLifetime.Singleton))",
            context.Output());
    }
}
