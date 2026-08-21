using CSharpAuthor.Tests.Adversary;
using Xunit;

namespace CSharpAuthor.Tests.DirectiveTests;

/// <summary>
/// <c>#region</c>, <c>#if</c> and <c>#line</c>. <see cref="PragmaOutputComponent"/> was previously
/// the only directive this library could write.
/// </summary>
public class DirectiveComponentTests
{
    private static ClassDefinition Method(string name)
    {
        var host = new ClassDefinition("Host");

        host.AddMethod(name);

        return host;
    }

    // ---- #region -------------------------------------------------------------------------------

    [Fact]
    public void RegionWrapsWhatItHolds()
    {
        var region = new RegionComponent("Generated members");

        region.Add(new MethodDefinition("First"));
        region.Add(new MethodDefinition("Second"));

        var emitted = Emit.Component(region).Replace("\r\n", "\n");

        Assert.StartsWith("#region Generated members\n", emitted);
        Assert.EndsWith("#endregion\n", emitted);
        Assert.Contains("public void First()", emitted);
        Assert.Contains("public void Second()", emitted);
    }

    [Fact]
    public void RegionWithoutALabelWritesTheBareDirective()
    {
        Assert.StartsWith("#region\n", Emit.Component(new RegionComponent()).Replace("\r\n", "\n"));
    }

    /// <summary>
    /// An unbalanced region is CS1038, reported at the end of the file rather than at the line that
    /// opened it - which is why the pairing is structural rather than two markers.
    /// </summary>
    [Fact]
    public void RegionInsideAClassCompiles()
    {
        var host = new ClassDefinition("Host");
        var region = new RegionComponent("Members");

        region.Add(new MethodDefinition("Work"));
        host.AddComponent(region);

        RoslynAssert.Compiles(Emit.Component(host));
    }

    // ---- #if -----------------------------------------------------------------------------------

    [Fact]
    public void ConditionalWritesEveryBranchInOrder()
    {
        var directive = new ConditionalDirectiveComponent("NET8_0_OR_GREATER");

        directive.If.Add(new MethodDefinition("Modern"));
        directive.ElseIf("NETSTANDARD2_0").Add(new MethodDefinition("Legacy"));
        directive.Else().Add(new MethodDefinition("Fallback"));

        var emitted = Emit.Component(directive).Replace("\r\n", "\n");

        Assert.Contains("#if NET8_0_OR_GREATER\n", emitted);
        Assert.Contains("#elif NETSTANDARD2_0\n", emitted);
        Assert.Contains("#else\n", emitted);
        Assert.EndsWith("#endif\n", emitted);

        Assert.True(
            emitted.IndexOf("Modern") < emitted.IndexOf("Legacy") &&
            emitted.IndexOf("Legacy") < emitted.IndexOf("Fallback"));
    }

    /// <summary>
    /// Asking twice returns the same branch. <c>IfElseLogicBlockDefinition.Else</c> discards and
    /// replaces, which is worth not repeating for something whose second copy is CS1571.
    /// </summary>
    [Fact]
    public void ElseAskedForTwiceIsOneBranch()
    {
        var directive = new ConditionalDirectiveComponent("DEBUG");

        directive.Else().Add(new MethodDefinition("First"));
        directive.Else().Add(new MethodDefinition("Second"));

        var emitted = Emit.Component(directive).Replace("\r\n", "\n");

        // One #else, holding both members - not two arms, which would be CS1571.
        Assert.Equal(1, emitted.Split(new[] { "#else\n" }, System.StringSplitOptions.None).Length - 1);
        Assert.Contains("First", emitted);
        Assert.Contains("Second", emitted);
    }

    [Fact]
    public void ConditionalMembersCompileInBothConfigurations()
    {
        var host = new ClassDefinition("Host");
        var directive = new ConditionalDirectiveComponent("SOME_SYMBOL");

        directive.If.Add(new MethodDefinition("Modern"));
        directive.Else().Add(new MethodDefinition("Fallback"));

        host.AddComponent(directive);

        RoslynAssert.Compiles(Emit.Component(host));
    }

    /// <summary>
    /// Every branch is written, so a type mentioned only in an excluded one still brings its
    /// import. That is the safe direction: an unneeded using is a hint, a missing one does not
    /// compile.
    /// </summary>
    [Fact]
    public void ATypeInAnyBranchStillDerivesItsImport()
    {
        var file = new CSharpFileDefinition("Sample");
        var host = file.AddClass("Host");

        var directive = new ConditionalDirectiveComponent("SOME_SYMBOL");
        var method = new MethodDefinition("Work");

        method.SetReturnType(TypeDefinition.Get("System.Text", "StringBuilder"));
        directive.Else().Add(method);

        host.AddComponent(directive);

        Assert.Contains("using System.Text;", Emit.File(file));
    }

    // ---- #line ---------------------------------------------------------------------------------

    [Fact]
    public void LineDirectiveForms()
    {
        Assert.Equal("#line 42\n", Emit.Component(LineDirectiveComponent.At(42)).Replace("\r\n", "\n"));
        Assert.Equal("#line default\n", Emit.Component(LineDirectiveComponent.Default()).Replace("\r\n", "\n"));
        Assert.Equal("#line hidden\n", Emit.Component(LineDirectiveComponent.Hidden()).Replace("\r\n", "\n"));
    }

    /// <summary>
    /// A Windows path is full of backslashes, and an unescaped one ends the literal early.
    /// </summary>
    [Fact]
    public void LineDirectiveEscapesItsFileName()
    {
        var emitted = Emit.Component(LineDirectiveComponent.At(7, @"C:\src\Models.cs"));

        Assert.Equal("#line 7 \"C:\\\\src\\\\Models.cs\"\n", emitted.Replace("\r\n", "\n"));
    }

    /// <summary>
    /// On a de-DE machine a grouped number would be written <c>#line 1.234</c>.
    /// </summary>
    [Fact]
    public void LineNumberIsCultureInvariant()
    {
        var emitted = Emit.InCulture("de-DE", () => Emit.Component(LineDirectiveComponent.At(1234)));

        Assert.Equal("#line 1234\n", emitted.Replace("\r\n", "\n"));
    }

    [Fact]
    public void LineDirectiveCompilesAroundAMember()
    {
        var host = new ClassDefinition("Host");

        host.AddComponent(LineDirectiveComponent.At(7, "Models.cs"));
        host.AddComponent(new MethodDefinition("Work"));
        host.AddComponent(LineDirectiveComponent.Default());

        RoslynAssert.Compiles(Emit.Component(host));
    }
}
