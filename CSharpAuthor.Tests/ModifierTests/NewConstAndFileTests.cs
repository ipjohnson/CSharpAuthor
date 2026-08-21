using System.Linq;
using CSharpAuthor.Profiles;
using CSharpAuthor.Tests.Adversary;
using Xunit;

namespace CSharpAuthor.Tests.ModifierTests;

/// <summary>
/// The three modifiers <see cref="ComponentModifier"/> could not previously say: <c>new</c>,
/// <c>const</c> and <c>file</c>.
/// </summary>
public class NewConstAndFileTests
{
    private static FieldDefinition Field(ComponentModifier modifiers, IOutputComponent? value = null)
    {
        var field = new FieldDefinition(TypeDefinition.Get(typeof(int)), "X")
        {
            Modifiers = modifiers,
            InitializeValue = value
        };

        return field;
    }

    // ---- new -----------------------------------------------------------------------------------

    /// <summary>
    /// Without it a generated member sharing a base member's name warns CS0108 on every build.
    /// </summary>
    [Fact]
    public void NewOnAMethodSuppressesTheHidingWarning()
    {
        var method = new MethodDefinition("ToString")
        {
            Modifiers = ComponentModifier.Public | ComponentModifier.New
        };

        method.SetReturnType(TypeDefinition.Get(typeof(string)));
        method.Return(SyntaxHelpers.QuoteString("x"));

        var emitted = Emit.Component(method);

        Assert.StartsWith("public new string ToString()", emitted);

        // The compiler is the judge: CS0108 as an error means the modifier did not take.
        RoslynAssert.MemberCompiles(emitted, warningsAsErrors: "CS0108");
    }

    [Fact]
    public void NewOnAPropertyAndAField()
    {
        var property = new PropertyDefinition(TypeDefinition.Get(typeof(int)), "Count")
        {
            Modifiers = ComponentModifier.Public | ComponentModifier.New
        };

        Assert.StartsWith("public new int Count", Emit.Component(property));
        Assert.StartsWith("public new int X", Emit.Component(Field(ComponentModifier.Public | ComponentModifier.New)));
    }

    [Fact]
    public void NewOnANestedType()
    {
        var nested = new ClassDefinition("Inner")
        {
            Modifiers = ComponentModifier.Public | ComponentModifier.New
        };

        Assert.StartsWith("public new class Inner", Emit.Component(nested));
    }

    // ---- const ---------------------------------------------------------------------------------

    /// <summary>
    /// Not the same declaration as <c>static readonly</c>: only a constant can be a <c>case</c>
    /// label, an attribute argument or a default parameter value.
    /// </summary>
    [Fact]
    public void ConstFieldIsUsableWhereAConstantIsRequired()
    {
        var field = Field(
            ComponentModifier.Public | ComponentModifier.Const,
            CodeOutputComponent.Get(1));

        var emitted = Emit.Component(field);

        Assert.Equal("public const int X = 1;\n", emitted.Replace("\r\n", "\n"));

        // A static readonly field in the same place is CS0150 - which is the whole point.
        RoslynAssert.MemberCompiles(
            emitted + "\npublic void M(int v) { switch (v) { case X: break; } }");
    }

    [Fact]
    public void ConstIsWrittenAfterNew()
    {
        var field = Field(
            ComponentModifier.Public | ComponentModifier.New | ComponentModifier.Const,
            CodeOutputComponent.Get(1));

        Assert.StartsWith("public new const int X", Emit.Component(field));
    }

    // ---- file ----------------------------------------------------------------------------------

    /// <summary>
    /// The accessibility a generator most wants for a helper type: two generators can each emit a
    /// <c>file class Helper</c> into one compilation without colliding.
    /// </summary>
    [Fact]
    public void FileLocalTypeCompiles()
    {
        var classDefinition = new ClassDefinition("Helper")
        {
            Modifiers = ComponentModifier.File
        };

        var emitted = Emit.Component(classDefinition);

        Assert.StartsWith("file class Helper", emitted);
        RoslynAssert.Compiles(emitted);
    }

    /// <summary>
    /// <c>file</c> replaces an accessibility level rather than joining one - <c>file internal</c>
    /// is CS9052 - so a caller that asked for both gets the narrower.
    /// </summary>
    [Fact]
    public void FileReplacesTheOtherAccessibilityLevels()
    {
        var classDefinition = new ClassDefinition("Helper")
        {
            Modifiers = ComponentModifier.File | ComponentModifier.Internal
        };

        var emitted = Emit.Component(classDefinition);

        Assert.StartsWith("file class Helper", emitted);
        RoslynAssert.Compiles(emitted);
    }

    /// <summary>
    /// There is no downlevel. <c>internal</c> is the nearest keyword and it publishes the type to
    /// the whole assembly, which is the silent widening this library refuses - so below C# 11 it is
    /// reported rather than quietly changed.
    /// </summary>
    [Fact]
    public void FileBelowCSharp11IsReportedRatherThanWidened()
    {
        var file = new CSharpFileDefinition("Sample");

        file.AddClass("Helper").Modifiers = ComponentModifier.File;

        var profile = new EmitProfile
        {
            Target = LanguageVersion.CSharp10,
            OnCapabilityViolation = CapabilityViolationBehavior.EmitErrorDirective
        };

        var result = ProfileEmitter.Emit(file, profile);

        Assert.True(result.HasErrors);

        Assert.Contains(
            result.Diagnostics,
            d => d.Feature == LanguageFeature.FileLocalTypes);

        // Not silently downgraded to internal, which would compile and be wrong.
        Assert.DoesNotContain("internal class Helper", result.Code);
    }

    [Fact]
    public void FileAtCSharp11IsAccepted()
    {
        var file = new CSharpFileDefinition("Sample");

        file.AddClass("Helper").Modifiers = ComponentModifier.File;

        var result = ProfileEmitter.Emit(
            file, new EmitProfile { Target = LanguageVersion.CSharp11 });

        Assert.False(result.HasErrors);
        Assert.Contains("file class Helper", result.Code);
    }
}
