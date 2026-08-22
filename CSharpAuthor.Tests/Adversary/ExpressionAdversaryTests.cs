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
    [Fact]
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
    [Fact]
    public void IsWithAGenericTypeThatHasANonGenericTwin()
    {
        var expression = Is(
            CodeOutputComponent.Get("x"),
            new GenericTypeDefinition(
                TypeDefinitionEnum.InterfaceDefinition, "System.Collections.Generic", "IEnumerable",
                new[] { TypeDefinition.Get(typeof(int)) }));

        Assert.Equal("x is IEnumerable<int>", Emit.Component(expression));
    }

    [Fact]
    public void IsWithAnArrayType()
    {
        var expression = Is(
            CodeOutputComponent.Get("x"),
            TypeDefinition.Get(typeof(string)).MakeArray());

        Assert.Equal("x is string[]", Emit.Component(expression));
    }

    [Fact]
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
    [Fact]
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

    /// <summary>
    /// <c>nameof</c> has an emitter - <see cref="CSharpAuthor.Profiles.NameOfStatement"/> - which
    /// the placeholder this replaces said did not exist. It is the expression a generated
    /// diagnostic or argument check is built from.
    /// </summary>
    [Fact]
    public void NameOf()
    {
        var emitted = Emit.Component(new CSharpAuthor.Profiles.NameOfStatement("value"));

        Assert.Equal("nameof(value)", emitted);
        RoslynAssert.StatementCompiles("var value = 1; var n = " + emitted + ";");
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
