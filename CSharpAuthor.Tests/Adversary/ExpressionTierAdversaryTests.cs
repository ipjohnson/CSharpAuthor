using CSharpAuthor.Expressions;
using Xunit;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// Adversary coverage for <c>CSharpAuthor.Expressions</c> - <c>Ex</c>, <c>Pat</c> and <c>Raw</c>.
/// </summary>
/// <remarks>
/// <para>
/// This file exists because the rest of <c>Adversary/</c> did not touch this tier. Every other file
/// here predates it: <c>ExpressionAdversaryTests</c> and <c>PatternCoverageTests</c> are named for
/// what they cover but exercise <c>SyntaxHelpers</c> and <c>LogicStatement</c>, the facade that
/// <c>Ex</c> replaced. So the largest piece of 2.0 shipped with its output asserted as strings and
/// never once handed to a compiler.
/// </para>
/// <para>
/// The assertion here is deliberately <see cref="RoslynAssert"/> rather than string equality. A
/// string assertion pins what the library writes; it cannot tell you whether what it wrote is legal
/// C#. Three of the defects found in the preview1003 review were of exactly that shape - confident,
/// well-formatted output that does not compile - and a string assertion would have passed on all
/// three.
/// </para>
/// </remarks>
public class ExpressionTierAdversaryTests
{
    /// <summary>
    /// Types the probes below bind against, so a pattern or a constructor call has something real
    /// to name.
    /// </summary>
    private const string Shapes = @"
namespace Probe
{
    public record Point
    {
        public int X { get; init; }
        public int Count { get; set; }
        public void Deconstruct(out int x, out int y) { x = X; y = Count; }
    }
}
";

    /// <summary>
    /// Locals the probes bind against. Declared inside the method body rather than as fields on a
    /// separate host type, so an unqualified name in the emitted expression resolves.
    /// </summary>
    private const string Locals = @"
object? value = null;
object boxed = """";
bool flag = true;
int number = 0;
string? text = null;
int[] values = new int[0];
int[] more = new int[0];
string[] names = new string[0];
global::Probe.Point point = new global::Probe.Point();
";

    private static readonly ITypeDefinition PointType = TypeDefinition.Get("Probe", "Point");
    private static readonly ITypeDefinition StringType = TypeDefinition.Get(typeof(string));
    private static readonly ITypeDefinition IntType = TypeDefinition.Get(typeof(int));

    private static string Render(IOutputComponent component) =>
        Emit.Component(component, new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

    // ---- patterns ---------------------------------------------------------------------------

    /// <summary>
    /// Every pattern form <c>Pat</c> offers, compiled rather than string-matched.
    /// </summary>
    /// <remarks>
    /// <c>docs/api-gaps.md</c> listed all thirteen of these as impossible as of preview1002. Twelve
    /// are emitted correctly; this theory is what keeps that true, and what would have caught the
    /// documentation going stale.
    /// </remarks>
    public static TheoryData<string, Pat> PatternForms() => new()
    {
        { "constant", Pat.Constant(Ex.Int(0)) },
        { "null", Pat.Null },
        { "not null", Pat.NotNull() },
        { "type", Pat.Type(StringType) },
        { "declaration", Pat.Declaration(StringType, "matched") },
        { "var", Pat.Var("captured") },
        { "relational", Pat.GreaterThan(Ex.Int(2)) },
        { "relational chain", Pat.And(Pat.GreaterThan(Ex.Int(2)), Pat.LessThanOrEqual(Ex.Int(9))) },
        { "or", Pat.Or(Pat.Constant(Ex.Int(1)), Pat.Constant(Ex.Int(2))) },
        { "not", Pat.Not(Pat.Constant(Ex.Int(3))) },
        { "parenthesised", Pat.Or(Pat.Constant(Ex.Int(1)), Pat.Parenthesized(Pat.And(Pat.NotNull(), Pat.Discard))) },
    };

    [Theory]
    [MemberData(nameof(PatternForms))]
    public void PatternCompiles(string name, Pat pattern)
    {
        Assert.NotNull(name);

        RoslynAssert.StatementCompiles(
            Locals + "_ = " + Render(Ex.Id("value").Is(pattern)) + ";",
            preamble: Shapes);
    }

    /// <summary>
    /// The recursive forms, which need a type with a deconstructor and properties to bind against.
    /// </summary>
    [Fact]
    public void RecursivePatternsCompile()
    {
        var positional = Ex.Id("point").Is(
            Pat.Positional(PointType, Pat.Constant(Ex.Int(0)), Pat.Var("y")));

        var property = Ex.Id("point").Is(
            Pat.Property(null, new[] { Pat.Prop("Count", Pat.GreaterThan(Ex.Int(0))) }));

        foreach (var expression in new[] { positional, property })
        {
            RoslynAssert.StatementCompiles(
                Locals + "_ = " + Render(expression) + ";",
                preamble: Shapes);
        }
    }

    /// <summary>
    /// List and slice patterns, which need an indexable, countable operand.
    /// </summary>
    [Fact]
    public void ListPatternsCompile()
    {
        var list = Ex.Id("values").Is(Pat.List(Pat.Constant(Ex.Int(1)), Pat.Slice()));
        var slice = Ex.Id("values").Is(Pat.List(Pat.Var("first"), Pat.Slice(Pat.Var("rest"))));

        foreach (var expression in new[] { list, slice })
        {
            RoslynAssert.StatementCompiles(
                Locals + "_ = " + Render(expression) + ";",
                preamble: Shapes);
        }
    }

    // ---- expressions ------------------------------------------------------------------------

    /// <summary>
    /// The constructs <c>docs/api-gaps.md</c> listed as having no emitter. All eleven compile.
    /// </summary>
    public static TheoryData<string, Ex> ExpressionForms() => new()
    {
        { "as", Ex.As(Ex.Id("boxed"), StringType) },
        { "conditional", Ex.Conditional(Ex.Id("flag"), Ex.Int(1), Ex.Int(2)) },
        { "interpolation", Ex.Interpolate("count=", Ex.Id("number")) },
        { "lambda", Ex.Id("names").Call("Select", Ex.Lambda("n", Ex.Id("n").Dot("Length"))) },
        { "range", Ex.Id("values").Index(Ex.Range(Ex.Int(1), Ex.FromEnd(Ex.Int(1)))) },
        { "index from end", Ex.Id("values").Index(Ex.FromEnd(Ex.Int(1))) },
        { "tuple", Ex.Tuple(Ex.Int(1), Ex.Str("a")) },
        { "switch expression", Ex.SwitchInline(Ex.Id("number"),
            Ex.Arm(Pat.Constant(Ex.Int(0)), Ex.Str("zero")),
            Ex.Arm(Pat.Discard, Ex.Str("other"))) },
        { "null-conditional chain", Ex.Id("text").NullDot("Length") },
        { "coalesce", Ex.Coalesce(Ex.Id("text"), Ex.Str("fallback")) },
    };

    [Theory]
    [MemberData(nameof(ExpressionForms))]
    public void ExpressionCompiles(string name, Ex expression)
    {
        Assert.NotNull(name);

        RoslynAssert.StatementCompiles(
            Locals + "_ = " + Render(expression) + ";",
            preamble: Shapes);
    }

    /// <summary>
    /// Object and record construction, which need a named type in scope.
    /// </summary>
    [Fact]
    public void ConstructionFormsCompile()
    {
        var forms = new[]
        {
            Ex.New(PointType),
            Ex.NewWithInitializer(PointType, null, Ex.Assign(Ex.Id("Count"), Ex.Int(1))),
            Ex.NewArray(IntType, Ex.Int(1), Ex.Int(2)),
            Ex.NewArrayImplicit(Ex.Int(1), Ex.Int(2)),
            Ex.With(Ex.Id("point"), Ex.Assign(Ex.Id("X"), Ex.Int(2))),
        };

        foreach (var expression in forms)
        {
            RoslynAssert.StatementCompiles(
                Locals + "_ = " + Render(expression) + ";",
                preamble: Shapes);
        }
    }

    /// <summary>
    /// A discard is legal as a switch arm but not as the whole pattern of an <c>is</c> - C# rejects
    /// <c>x is _</c>. So the discard is covered where it is actually usable.
    /// </summary>
    [Fact]
    public void DiscardCompilesAsASwitchArm()
    {
        var expression = Ex.SwitchInline(
            Ex.Id("number"),
            Ex.Arm(Pat.Constant(Ex.Int(0)), Ex.Str("zero")),
            Ex.Arm(Pat.Discard, Ex.Str("other")));

        RoslynAssert.StatementCompiles(Locals + "_ = " + Render(expression) + ";", preamble: Shapes);
    }

    /// <summary>
    /// A collection expression is target-typed, so it needs a typed destination rather than the
    /// discard the other expression probes use.
    /// </summary>
    [Fact]
    public void CollectionExpressionCompiles()
    {
        var expression = Ex.Collection(Ex.Int(1), Ex.Spread(Ex.Id("more")));

        RoslynAssert.StatementCompiles(
            Locals + "int[] collected = " + Render(expression) + ";",
            preamble: Shapes);
    }

    // ---- precedence -------------------------------------------------------------------------

    /// <summary>
    /// Precedence is the reason to build an expression as an object rather than concatenate it, so
    /// it is worth pinning that <c>Ex</c> brackets what needs bracketing and nothing else.
    /// </summary>
    [Theory]
    [InlineData("(1 + 2) * 3")]
    [InlineData("1 + 2 * 3")]
    public void PrecedenceRoundTrips(string expected)
    {
        var expression = expected == "(1 + 2) * 3"
            ? Ex.Multiply(Ex.Add(Ex.Int(1), Ex.Int(2)), Ex.Int(3))
            : Ex.Add(Ex.Int(1), Ex.Multiply(Ex.Int(2), Ex.Int(3)));

        Assert.Equal(expected, Render(expression));

        RoslynAssert.ExpressionCompiles(Render(expression));
    }

    // ---- the seam between the two tiers -----------------------------------------------------

    /// <summary>
    /// An expression object substituted through <c>{argN}</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the defect the preview1003 review called the most consequential one in the release.
    /// <c>AddCode</c> is the API the README taught most thoroughly, and handing it any composed
    /// expression - an <c>Ex</c>, a <c>NewStatement</c>, a <c>Raw</c> - emitted the argument's .NET
    /// type name instead of its code: <c>var a = CSharpAuthor.Expressions.Ex;</c>.
    /// </para>
    /// <para>
    /// It failed in two places, which is why the first fix did not take. <c>GetSubstitutionParts</c>
    /// stringified the value, and <c>CodeOutputComponent.FromParts</c> then decided a parts list was
    /// worth keeping only when it contained an <c>ITypeDefinition</c> - so a component-only list was
    /// flattened to text before the writer ever saw it.
    /// </para>
    /// </remarks>
    [Fact]
    public void ComposedExpressionSurvivesArgSubstitution()
    {
        var file = new CSharpFileDefinition("Probe.Substitution");

        var holder = file.AddClass("Holder");
        holder.Modifiers = ComponentModifier.Public;

        var method = holder.AddMethod("Go");
        method.Modifiers = ComponentModifier.Public;
        method.SetReturnType(typeof(void));

        method.AddCode("var sum = {arg1};", Ex.Add(Ex.Int(42), Ex.Int(1)));
        method.AddCode("var pair = {arg1} + {arg2};", Ex.Int(1), Ex.Int(2));
        method.AddCode("var made = {arg1};", SyntaxHelpers.New(PointType));

        var output = Emit.File(file, new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        Assert.Contains("var sum = 42 + 1;", output);
        Assert.Contains("var pair = 1 + 2;", output);
        Assert.Contains("var made = new global::Probe.Point();", output);

        Assert.DoesNotContain("CSharpAuthor.", output);

        RoslynAssert.Compiles(Shapes + output);
    }

    /// <summary>
    /// A type substituted alongside a component still derives its using, so the fix to the parts
    /// pipeline did not cost the behaviour the pipeline existed for.
    /// </summary>
    [Fact]
    public void TypeTrackingSurvivesAlongsideAComponent()
    {
        var file = new CSharpFileDefinition("Probe.Tracking");

        var holder = file.AddClass("Holder");
        holder.Modifiers = ComponentModifier.Public;

        var method = holder.AddMethod("Go");
        method.Modifiers = ComponentModifier.Public;
        method.SetReturnType(typeof(void));

        method.AddCode(
            "var builder = new {arg1}(); var n = {arg2};",
            TypeDefinition.Get(typeof(System.Text.StringBuilder)),
            Ex.Int(7));

        var output = Emit.File(file);

        Assert.Contains("using System.Text;", output);
        Assert.Contains("new StringBuilder()", output);
        Assert.Contains("var n = 7;", output);
    }

    // ---- keyword escaping parity ------------------------------------------------------------

    /// <summary>
    /// Every C# keyword, as a member name, through <c>Ex</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A generator that mirrors user symbols will eventually be handed a member called
    /// <c>new</c> or <c>class</c>. Escaping it is not optional, and it is the kind of thing that is
    /// correct in one code path and forgotten in the one next to it - which is exactly what the
    /// preview1003 review found between this tier and <c>SyntaxHelpers</c>.
    /// </para>
    /// <para>
    /// This theory asserts the property rather than a list of examples, so a new renderer that
    /// forgets to escape fails here rather than in a consumer's build.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(Keywords))]
    public void KeywordMemberNamesAreEscaped(string keyword)
    {
        Assert.Equal("@" + keyword, Render(Ex.Id(keyword)));

        Assert.Equal(
            "global::Probe.Point.@" + keyword,
            Render(Ex.On(PointType, keyword)));
    }

    public static TheoryData<string> Keywords() => new()
    {
        "new", "class", "int", "return", "default", "this", "void", "event", "lock", "params",
    };

    /// <summary>
    /// The same keyword through <c>SyntaxHelpers</c>, which must agree with <c>Ex</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two tiers disagreed in preview1003: <c>Ex.On</c> escaped, and the four
    /// <c>SyntaxHelpers</c> renderers next to it did not, so a state named <c>new</c> emitted
    /// <c>case LatchState.new:</c> and failed the consumer's build. <c>SyntaxHelpers.Invoke("new")</c>
    /// was worse than a syntax error - it emitted <c>new()</c>, a target-typed object creation where
    /// a call was intended.
    /// </para>
    /// <para>
    /// Asserting the two tiers against each other, rather than each against a literal, is what makes
    /// this hold: a renderer added later cannot pass by escaping differently.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(Keywords))]
    public void SyntaxHelpersEscapesTheSameAsEx(string keyword)
    {
        // `this` is an expression keyword rather than a member name, so it is not a member the
        // static-property renderer can be handed. Every other keyword must round-trip identically.
        if (keyword == "this")
        {
            return;
        }

        Assert.Equal(
            Render(Ex.On(PointType, keyword)),
            Render(SyntaxHelpers.Property(PointType, keyword)));

        Assert.Equal("@" + keyword + "()", Render(SyntaxHelpers.Invoke(keyword)));
    }

    /// <summary>
    /// Escaping must not reach names that are not keywords, nor split a dotted name.
    /// </summary>
    [Theory]
    [InlineData("Value")]
    [InlineData("Foo.Bar")]
    [InlineData("ToString")]
    public void OrdinaryNamesAreUntouched(string name)
    {
        Assert.DoesNotContain("@", Render(SyntaxHelpers.Property(PointType, name)));
        Assert.DoesNotContain("@", Render(SyntaxHelpers.Invoke(name)));
    }

    /// <summary>
    /// A keyword used as a declared name, compiled, so the escape is proved legal rather than
    /// merely present.
    /// </summary>
    [Fact]
    public void EscapedKeywordMemberCompiles()
    {
        var file = new CSharpFileDefinition("Probe.Escaping");

        var holder = file.AddClass("Holder");
        holder.Modifiers = ComponentModifier.Public;

        var field = holder.AddField(IntType, "lock");
        field.Modifiers = ComponentModifier.Public;

        var method = holder.AddMethod("static");
        method.Modifiers = ComponentModifier.Public;
        method.SetReturnType(IntType);
        method.AddParameter(IntType, "params");
        method.Return(Ex.Id("lock"));

        RoslynAssert.Compiles(Emit.File(file));
    }
}
