using System;
using Xunit;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// <c>CodeOutputComponent.Get(object)</c> — the single funnel every value passes through on its way
/// to becoming text.
/// </summary>
/// <remarks>
/// <para>
/// The last case in <c>DefaultComponent</c> is <c>value.ToString()</c>. That is the root of most of
/// the literal findings: <c>ToString</c> answers "how does this look to a person", and the question
/// being asked is "what is the C# for this". For <c>bool</c> and <c>int</c> the two answers happen
/// to coincide. For everything else they do not, and nothing distinguishes the cases where they
/// diverge from the cases where they do not.
/// </para>
/// <para>
/// The enum case is the one the handoff already tells a story about. §1 traces a stray
/// <c>using Microsoft.Extensions.DependencyInjection</c> in a Global-mode file to
/// <c>CodeOutputComponent.Get("ServiceLifetime.Transient")</c> — a raw string tracking no namespace.
/// <see cref="EnumValue"/> is the same defect one step earlier: the caller does not even have to
/// reach for a string, because handing over the enum value itself produces a bare member name.
/// </para>
/// </remarks>
public class ValueConversionAdversaryTests
{
    public enum Lifetime
    {
        Transient,
        Singleton
    }

    /// <summary>
    /// An enum value becomes its bare member name, with no type in front of it and no namespace
    /// recorded — the §1 defect, reachable without writing a string at all.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: CodeOutputComponent.Get(Lifetime.Singleton) emits 'Singleton' - the member name alone, unqualified, with no namespace tracked. CS0103 in the ordinary case, and where a local of that name exists it compiles and means something else. This is the §1 ServiceLifetime defect at its source.")]
    public void EnumValue()
    {
        var output = Emit.Component(CodeOutputComponent.Get(Lifetime.Singleton));

        Assert.Contains("Lifetime.Singleton", output);
    }

    [Fact(Skip = "ADVERSARY GAP: an enum value passed to AddCode's {argN} emits the bare member name - 'var x = Singleton;' - CS0103")]
    public void EnumValueThroughAddCode()
    {
        var method = new MethodDefinition("M");

        method.AddCode("var x = {arg1};", Lifetime.Singleton);

        RoslynAssert.MemberCompiles(
            Emit.Component(method),
            preamble: "public enum Lifetime { Transient, Singleton }\n");
    }

    /// <summary>
    /// A two-dimensional array is flattened. <c>DefaultComponent</c> iterates the array, which for a
    /// rank-2 array yields every element in row-major order, and writes them into a
    /// single-dimensional initializer.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: a rank-2 array is flattened into a rank-1 initializer - new int[2,2]{{1,2},{3,4}} emits 'new int[] { 1, 2, 3, 4 }', a different value of a different type (CS0029 when assigned to int[,])")]
    public void TwoDimensionalArrayValue()
    {
        var output = Emit.Component(CodeOutputComponent.Get(new[,] { { 1, 2 }, { 3, 4 } }));

        Assert.Equal("new int[,] { { 1, 2 }, { 3, 4 } }", output);
    }

    /// <summary>
    /// An empty array writes neither a size nor an initializer.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: an empty array emits 'new int[]' - no size, no initializer - which is CS1586")]
    public void EmptyArrayValue()
    {
        RoslynAssert.ExpressionCompiles(Emit.Component(CodeOutputComponent.Get(new int[0])));
    }

    /// <summary>
    /// A string handed to <c>AddAttribute</c> is written as code rather than as a literal.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: AddAttribute routes its arguments through CodeOutputComponent.Get, which writes a string as code - AddAttribute(type, \"hello\") emits [My(hello)], CS0103 - while AddCode's {argN} quotes the same value")]
    public void StringAttributeArgument()
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.AddAttribute(TypeDefinition.Get("Probe", "MyAttribute"), "hello");

        Assert.Contains("[My(\"hello\")]", Emit.Component(classDefinition));
    }

    /// <summary>
    /// A placeholder with no matching argument is written into the output verbatim, so a typo or an
    /// off-by-one in the argument list reaches the generated file as text.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: AddCode leaves an unmatched {argN} or [argN] placeholder in the output as literal text rather than reporting it - 'X(1,[arg9]);' reaches the generated file")]
    public void UnmatchedPlaceholder()
    {
        var method = new MethodDefinition("M");

        method.AddCode("X([arg1],[arg9]);", 1, 99);

        Assert.DoesNotContain("[arg9]", Emit.Component(method));
    }

    // ---- conversions that are correct, kept as guards ----

    [Fact]
    public void BoolValueUsesTheCSharpLiteral()
    {
        Assert.Equal("true", Emit.Component(CodeOutputComponent.Get(true)));
        Assert.Equal("false", Emit.Component(CodeOutputComponent.Get(false)));
    }

    [Fact]
    public void IntValueIsWrittenAsIs()
    {
        Assert.Equal("42", Emit.Component(CodeOutputComponent.Get(42)));
    }

    [Fact]
    public void NonEmptyArrayValueCompiles()
    {
        RoslynAssert.ExpressionCompiles(Emit.Component(CodeOutputComponent.Get(new[] { 1, 2, 3 })));
    }

    [Fact]
    public void StringArrayValueCompiles()
    {
        RoslynAssert.ExpressionCompiles(
            Emit.Component(CodeOutputComponent.Get(new[] { "a", "b" })));
    }

    /// <summary>
    /// A component handed to <c>Get</c> is passed through untouched, which is what makes the escape
    /// hatch compose. Guard.
    /// </summary>
    [Fact]
    public void AComponentIsPassedThrough()
    {
        var component = new CodeOutputComponent("already code") { Indented = false };

        Assert.Same(component, CodeOutputComponent.Get(component));
    }

    /// <summary>
    /// A matched placeholder substitutes and records the type, so the namespace is imported. Guard —
    /// the eager rendering is recorded separately in
    /// <see cref="OutputContextAdversaryTests.AddCodeDefersItsTypes"/>.
    /// </summary>
    [Fact]
    public void MatchedTypePlaceholderImportsItsNamespace()
    {
        var file = new CSharpFileDefinition("Consumer");

        file.AddClass("Host").AddMethod("M")
            .AddCode("var x = new {arg1}();", TypeDefinition.Get("Far.Away", "Thing"));

        Assert.Contains("using Far.Away;", Emit.File(file));
    }
}
