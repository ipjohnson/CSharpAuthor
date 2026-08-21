using System;
using System.Collections.Generic;
using Xunit;
using static CSharpAuthor.SyntaxHelpers;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// Expressions: what <c>SyntaxHelpers</c> writes, and the forms it has no way to write.
/// </summary>
/// <remarks>
/// The gaps in this file split into two kinds and the distinction matters for whoever fixes them.
/// A defect is a method that exists and writes the wrong thing. A coverage hole is a form of C# with
/// no entry point at all - reachable only by handing a string to <c>AddCode</c>, which is the escape
/// hatch, not the feature.
/// </remarks>
public class ExpressionAdversaryTests
{
    // ---- defects in what exists ----

    /// <summary>
    /// <c>Is</c> writes <c>ITypeDefinition.Name</c> rather than asking the type to render itself, so
    /// a generic type loses its arguments, an array loses its brackets, and the output mode never
    /// reaches it.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: SyntaxHelpers.Is writes typeDefinition.Name, so 'x is List<int>' is emitted 'x is List' - a different type where a non-generic one exists (IEnumerable, Task, Nullable), and CS0246 where it does not")]
    public void IsWithAGenericType()
    {
        var expression = Is(
            CodeOutputComponent.Get("x"),
            new GenericTypeDefinition(
                TypeDefinitionEnum.ClassDefinition, "System.Collections.Generic", "List",
                new[] { TypeDefinition.Get(typeof(int)) }));

        Assert.Equal("x is List<int>", Emit.Component(expression));
    }

    /// <summary>
    /// The same defect where the wrong reading compiles. <c>IEnumerable</c> and
    /// <c>IEnumerable&lt;int&gt;</c> both exist, so <c>x is IEnumerable&lt;int&gt;</c> silently
    /// becomes a test against the non-generic interface - true for a <c>List&lt;string&gt;</c>,
    /// which the caller was trying to exclude.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: 'x is IEnumerable<int>' is emitted 'x is IEnumerable', which compiles and is true for values the caller meant to reject")]
    public void IsWithAGenericTypeThatHasANonGenericTwin()
    {
        var expression = Is(
            CodeOutputComponent.Get("x"),
            new GenericTypeDefinition(
                TypeDefinitionEnum.InterfaceDefinition, "System.Collections.Generic", "IEnumerable",
                new[] { TypeDefinition.Get(typeof(int)) }));

        Assert.Equal("x is IEnumerable<int>", Emit.Component(expression));
    }

    [Fact(Skip = "ADVERSARY GAP: SyntaxHelpers.Is writes Name, so an array type loses its brackets - 'x is string[]' is emitted 'x is string'")]
    public void IsWithAnArrayType()
    {
        var expression = Is(
            CodeOutputComponent.Get("x"),
            TypeDefinition.Get(typeof(string)).MakeArray());

        Assert.Equal("x is string[]", Emit.Component(expression));
    }

    [Fact(Skip = "ADVERSARY GAP: SyntaxHelpers.Is bypasses IOutputContext.Write(ITypeDefinition), so TypeOutputMode never reaches it - the type stays a bare short name in a Global-mode file")]
    public void IsInGlobalMode()
    {
        var expression = Is(CodeOutputComponent.Get("x"), TypeDefinition.Get("Probe", "Dog"));

        var output = Emit.Component(
            expression, new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        Assert.Equal("x is global::Probe.Dog", output);
    }

    /// <summary>
    /// <c>Is</c> calls <c>AddUsingNamespace</c> on itself, which is the call the handoff's first
    /// invariant forbids: a namespace must be derived from a type that was written, not asserted
    /// alongside one. Here it is also incomplete - only the outer type's namespace is added, so a
    /// generic argument from a third namespace is never imported.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: Is adds only the outer type's namespace by hand (violating invariant 1), so a generic argument's namespace is never imported")]
    public void IsImportsItsTypeArgumentsNamespaces()
    {
        var file = new CSharpFileDefinition("Consumer");

        var method = file.AddClass("Host").AddMethod("M");

        method.Add(new IndentedStatementComponent(Is(
            CodeOutputComponent.Get("x"),
            new GenericTypeDefinition(
                TypeDefinitionEnum.ClassDefinition, "Outer.Space", "Wrapper",
                new[] { TypeDefinition.Get("Inner.Space", "Payload") }))));

        Assert.Contains("using Inner.Space;", Emit.File(file));
    }

    /// <summary>
    /// The one shape <c>Is</c> gets right, which the existing suite already covers. Guard.
    /// </summary>
    [Fact]
    public void IsWithASimpleType()
    {
        Assert.Equal(
            "x is object",
            Emit.Component(Is(CodeOutputComponent.Get("x"), TypeDefinition.Get(typeof(object)))));
    }

    // ---- expressions with no entry point at all ----

    [Fact(Skip = "ADVERSARY GAP: no API - there is no lambda emitter. None of the four forms (x => e, (x) => e, (T x) => e, delegate { }) can be built, so any generator emitting a LINQ query, an event handler or a factory has to hand AddCode a string.")]
    public void Lambdas()
    {
        Assert.True(false, "no API for lambda expressions");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - there is no switch expression emitter. SwitchBlockDefinition writes the statement form only, so 'x switch { ... }' cannot be produced.")]
    public void SwitchExpressions()
    {
        Assert.True(false, "no API for switch expressions");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - there is no interpolated string emitter, so $\"{a}\" cannot be built, and neither can the alignment/format clauses ({a,-10:N2}) or the escaping a nested quote needs")]
    public void InterpolatedStrings()
    {
        Assert.True(false, "no API for interpolated strings");
    }

    /// <summary>
    /// A tuple has no type either: <c>ITypeDefinition</c> can name <c>ValueTuple&lt;int,string&gt;</c>
    /// but not <c>(int Count, string Name)</c>, and the element names are the entire point.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: no API - a tuple type cannot be expressed. ValueTuple<int,string> can, but (int Count, string Name) cannot, so the element names are unreachable; nor is there a tuple literal or a deconstruction.")]
    public void TuplesAndDeconstruction()
    {
        Assert.True(false, "no API for tuple types, tuple literals or deconstruction");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - there is no 'with' expression emitter, so a record cannot be copied with a change, which is the reason records were used in the first place")]
    public void WithExpressions()
    {
        Assert.True(false, "no API for with-expressions");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - there is no range or index emitter. IndexStatement writes x[i]; x[1..^1] and x[^1] have no component.")]
    public void RangesAndIndices()
    {
        Assert.True(false, "no API for ranges or from-end indices");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - there is no collection expression emitter, so [1, 2, ..rest] cannot be built. NewArrayStatement writes the new T[] { } form only.")]
    public void CollectionExpressionsAndSpreads()
    {
        Assert.True(false, "no API for collection expressions or spread elements");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - there is no stackalloc emitter")]
    public void StackAlloc()
    {
        Assert.True(false, "no API for stackalloc");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - there is no nameof emitter, which is the expression a generated diagnostic or argument check needs most")]
    public void NameOf()
    {
        Assert.True(false, "no API for nameof");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - there is no conditional (?:) emitter; LogicStatement takes one infix operator and a ternary needs two")]
    public void ConditionalExpression()
    {
        Assert.True(false, "no API for the conditional operator");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - there is no 'as' emitter, nor a safe-cast pairing with 'is'")]
    public void AsExpression()
    {
        Assert.True(false, "no API for the as operator");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - there is no object or collection initializer emitter that names members. NewStatement.AddInitValue writes bare values into { }, so 'new Foo { Bar = 1 }' can only be produced by passing the whole assignment as a preformatted string.")]
    public void ObjectInitializerWithNamedMembers()
    {
        Assert.True(false, "no API for named-member object initializers");
    }

    // ---- expressions that do work, kept as guards ----

    [Fact]
    public void ObjectCreationCompiles()
    {
        RoslynAssert.ExpressionCompiles(
            Emit.Component(New(typeof(List<int>))), preamble: "");
    }

    [Fact]
    public void TypeofCompiles()
    {
        RoslynAssert.ExpressionCompiles(
            Emit.Component(TypeOf(TypeDefinition.Get(typeof(string)))));
    }

    [Fact]
    public void ArrayCreationWithValuesCompiles()
    {
        RoslynAssert.ExpressionCompiles(Emit.Component(NewArray(typeof(int), 1, 2, 3)));
    }

    [Fact]
    public void ArrayCreationWithLengthCompiles()
    {
        RoslynAssert.ExpressionCompiles(Emit.Component(NewArray(typeof(int), 5)));
    }

    [Fact]
    public void StaticInvokeCompiles()
    {
        RoslynAssert.ExpressionCompiles(
            Emit.Component(Invoke(TypeDefinition.Get("System", "String"), "Concat",
                CodeOutputComponent.Get(QuoteString("a")), CodeOutputComponent.Get(QuoteString("b")))));
    }

    [Fact]
    public void GenericInvokeCompiles()
    {
        RoslynAssert.ExpressionCompiles(
            Emit.Component(InvokeGeneric(
                TypeDefinition.Get("System", "Array"), "Empty",
                new[] { TypeDefinition.Get(typeof(int)) })));
    }
}
