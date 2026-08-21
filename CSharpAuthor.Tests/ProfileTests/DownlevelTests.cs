using System.Linq;
using Xunit;

namespace CSharpAuthor.Tests.ProfileTests;

/// <summary>
/// The free downlevels: one tree, two renderings, the same meaning.
/// </summary>
public class DownlevelTests
{
    private static readonly EmitProfile Modern = EmitProfile.Default;

    private static readonly EmitProfile Old =
        EmitProfile.Conservative.With(p =>
        {
            p.PreferCollectionExprs = true;
            p.PreferTargetTypedNew = true;
            p.PreferRawStrings = true;
        });

    private static string Emit(IOutputComponent component, EmitProfile profile) =>
        ProfileEmitter.Emit(component, profile).Code;

    [Fact]
    public void ACollectionIsABracketListOrAnArray()
    {
        var collection = CollectionExpressionStatement.Of(TypeDefinition.Get(typeof(int)), 1, 2, 3);

        Assert.Equal("[1, 2, 3]", Emit(collection, Modern));
        Assert.Equal("new int[] { 1, 2, 3 }", Emit(collection, Old));
    }

    [Fact]
    public void ACollectionWithNoElementTypeInfersOne()
    {
        var collection = CollectionExpressionStatement.Of(null, 1, 2);

        Assert.Equal("[1, 2]", Emit(collection, Modern));
        Assert.Equal("new[] { 1, 2 }", Emit(collection, Old));
    }

    [Fact]
    public void AnEmptyCollectionWithNoElementTypeCannotBeDownlevelled()
    {
        // `new[] { }` is CS0826. Picking `object` would compile and be a different collection, so
        // this stops rather than substitutes.
        var collection = CollectionExpressionStatement.Of(null);

        Assert.Equal("[]", Emit(collection, Modern));

        var result = ProfileEmitter.Emit(
            collection, Old.With(p => p.OnCapabilityViolation = CapabilityViolationBehavior.EmitErrorDirective));

        Assert.True(result.HasErrors);
        Assert.Contains(EmitDiagnostic.NoDownlevelAvailableId, result.Code);
    }

    [Fact]
    public void AnEmptyCollectionWithAnElementTypeIsFine()
    {
        var collection = CollectionExpressionStatement.Of(TypeDefinition.Get(typeof(int)));

        Assert.Equal("new int[] { }", Emit(collection, Old));
    }

    [Fact]
    public void TargetTypedNewNamesTheTypeWhenItHasTo()
    {
        var construction = TargetTypedNewStatement.Of(TypeDefinition.Get(typeof(System.Text.StringBuilder)));

        Assert.Equal("new()", Emit(construction, Modern));
        Assert.Equal("new StringBuilder()", Emit(construction, Old));
    }

    [Fact]
    public void TheTargetTypedFormClaimsNoUsing()
    {
        // Invariant 1: a namespace is imported because a type was written, never because a writer
        // said so. The target-typed form writes no type, so it needs no using - and the field's own
        // type declaration is what brings one in.
        var context = new ProfiledOutputContext(Modern);

        TargetTypedNewStatement.Of(TypeDefinition.Get(typeof(System.Text.StringBuilder))).WriteOutput(context);
        context.GenerateUsingStatements();

        Assert.Equal("new()", context.Output());

        var older = new ProfiledOutputContext(Old);

        TargetTypedNewStatement.Of(TypeDefinition.Get(typeof(System.Text.StringBuilder))).WriteOutput(older);
        older.GenerateUsingStatements();

        Assert.Contains("using System.Text;", older.Output());
    }

    [Fact]
    public void NameOfBecomesTheStringItWouldHaveProduced()
    {
        Assert.Equal("nameof(Widget)", Emit(new NameOfStatement("Widget"), Modern));
        Assert.Equal(
            "\"Widget\"",
            Emit(new NameOfStatement("Widget"), EmitProfile.Conservative.With(p => p.Target = LanguageVersion.CSharp5)));
    }

    [Fact]
    public void NameOfADottedNameIsItsLastSegment()
    {
        // nameof(A.B.C) is "C". Emitting "A.B.C" would be a different string.
        Assert.Equal(
            "\"Deepest\"",
            Emit(
                new NameOfStatement("Outer.Inner.Deepest"),
                EmitProfile.Conservative.With(p => p.Target = LanguageVersion.CSharp5)));
    }

    [Fact]
    public void AUsingDeclarationBecomesAUsingBlock()
    {
        var statement = new UsingDeclarationStatement("var stream = OpenFile()");

        statement.AddCode("Read(stream);");

        AssertEqual.WithoutNewLine(
            "using var stream = OpenFile();\nRead(stream);\n",
            Emit(statement, EmitProfile.Default));

        AssertEqual.WithoutNewLine(
            "using (var stream = OpenFile())\n{\n    Read(stream);\n}\n",
            Emit(statement, EmitProfile.Conservative.With(p => p.Target = LanguageVersion.CSharp7_3)));
    }

    [Fact]
    public void ALabeledBreakBecomesAGoto()
    {
        AssertEqual.WithoutNewLine(
            "outer:\nforeach (var row in grid)\n{\n    break outer;\n}\n",
            Emit(LabeledLoop(), EmitProfile.Latest));

        AssertEqual.WithoutNewLine(
            "foreach (var row in grid)\n{\n}\nouter_break: ;\n",
            Emit(LabeledLoopWithNothingButAJump(), EmitProfile.Default));
    }

    [Fact]
    public void ALabeledContinueGetsItsLabelAtTheEndOfTheBody()
    {
        var loop = new LabeledLoopStatement("outer", "foreach (var row in grid)");

        loop.Add(new LabeledJumpStatement(LabeledJumpKind.Continue, "outer"));

        AssertEqual.WithoutNewLine(
            "foreach (var row in grid)\n{\n    goto outer_continue;\n    outer_continue: ;\n}\n",
            Emit(loop, EmitProfile.Default));
    }

    [Fact]
    public void ALabelNothingJumpsToIsNotDeclared()
    {
        // Declaring both synthetic labels every time would trade a language feature for a pair of
        // CS0164 warnings on every loop.
        var loop = new LabeledLoopStatement("outer", "foreach (var row in grid)");

        loop.AddCode("Use(row);");

        var code = Emit(loop, EmitProfile.Default);

        Assert.DoesNotContain("outer_break", code);
        Assert.DoesNotContain("outer_continue", code);
    }

    [Fact]
    public void ASwitchExpressionBecomesAConditionalChain()
    {
        var expression = new SwitchExpressionStatement("n")
            .AddArm("1", "\"one\"")
            .AddArm("2", "\"two\"")
            .Otherwise("null");

        Assert.Equal("n switch { 1 => \"one\", 2 => \"two\", _ => null }", Emit(expression, Modern));
        Assert.Equal("n == 1 ? \"one\" : n == 2 ? \"two\" : null", Emit(expression, ProfileFor(LanguageVersion.CSharp7_3)));
    }

    [Fact]
    public void ASwitchOnPatternsHasNoConditionalForm()
    {
        var expression = new SwitchExpressionStatement("shape")
            .AddArm("Circle c", "c.Radius")
            .Otherwise("0");

        expression.ArmsAreEqualityTests = false;

        var result = ProfileEmitter.Emit(
            expression,
            ProfileFor(LanguageVersion.CSharp7_3)
                .With(p => p.OnCapabilityViolation = CapabilityViolationBehavior.EmitErrorDirective));

        Assert.True(result.HasErrors);
        Assert.Contains("equality tests against constants", result.Code);
    }

    [Fact]
    public void AStringIsEscapedRatherThanCopied()
    {
        // V1 wraps the value in quotes and escapes nothing: `"he said "hi""` is CS1002.
        var literal = new StringLiteralStatement("he said \"hi\"");

        Assert.Equal("\"he said \\\"hi\\\"\"", Emit(literal, Modern));
    }

    [Theory]
    [InlineData("plain", "\"plain\"")]
    [InlineData("with\\backslash", "\"with\\\\backslash\"")]
    [InlineData("line\nbreak", "\"line\\nbreak\"")]
    [InlineData("tab\there", "\"tab\\there\"")]
    [InlineData("null\0char", "\"null\\0char\"")]
    [InlineData("quote\"inside", "\"quote\\\"inside\"")]
    public void EveryEscapeIsTheOneCSharpUses(string value, string expected)
    {
        Assert.Equal(expected, StringLiteralStatement.Quote(value));
    }

    [Fact]
    public void ARawLiteralIsUsedOnlyWhereItHelpsAndIsLegal()
    {
        var profile = EmitProfile.Default.With(p => p.PreferRawStrings = true);

        // Nothing to escape: three quotes would be noise.
        Assert.Equal("\"plain\"", Emit(new StringLiteralStatement("plain"), profile));

        // Something to escape, and nothing at either end that the fence could swallow: the raw
        // form earns its place.
        Assert.Equal(
            "\"\"\"he said \"hi\" loudly\"\"\"",
            Emit(new StringLiteralStatement("he said \"hi\" loudly"), profile));
    }

    [Fact]
    public void ARawLiteralIsNotUsedWhenItWouldBeCS8998()
    {
        // A single-line raw literal whose content ends in a quote cannot be fenced: the padding
        // trick that looks like it works changes the value.
        var profile = EmitProfile.Default.With(p => p.PreferRawStrings = true);

        Assert.Equal("\"ends with \\\"\"", Emit(new StringLiteralStatement("ends with \""), profile));
        Assert.False(StringLiteralStatement.CanBeWrittenRaw("\"starts with a quote"));
    }

    [Fact]
    public void APreferenceThatCannotBeHonouredIsNotAnError()
    {
        var result = ProfileEmitter.Emit(
            CollectionExpressionStatement.Of(TypeDefinition.Get(typeof(int)), 1, 2),
            Old);

        Assert.False(result.HasErrors);
        Assert.All(result.Diagnostics, d => Assert.Equal(EmitSeverity.Info, d.Severity));
        Assert.Empty(result.DownlevelNotes);
    }

    private static EmitProfile ProfileFor(LanguageVersion version) =>
        EmitProfile.Default.With(p => p.Target = version);

    private static LabeledLoopStatement LabeledLoop()
    {
        var loop = new LabeledLoopStatement("outer", "foreach (var row in grid)");

        loop.Add(new LabeledJumpStatement(LabeledJumpKind.Break, "outer"));

        return loop;
    }

    private static LabeledLoopStatement LabeledLoopWithNothingButAJump()
    {
        var loop = new LabeledLoopStatement("outer", "foreach (var row in grid)");

        var inner = new CodeOutputComponent("");

        loop.Add(new BreakOutsideTheBody());

        return loop;
    }

    /// <summary>
    /// A jump written before the loop's body is closed, standing in for one that a nested
    /// statement made: the loop only learns which labels it needs while writing its body.
    /// </summary>
    private class BreakOutsideTheBody : IOutputComponent
    {
        public void AddUsingNamespace(string ns)
        {
        }

        public void WriteOutput(IOutputContext outputContext) =>
            outputContext.EmitSession().MarkLabelUsed("outer_break");
    }
}
