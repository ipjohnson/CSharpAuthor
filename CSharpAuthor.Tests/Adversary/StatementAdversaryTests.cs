using System;
using Xunit;
using static CSharpAuthor.SyntaxHelpers;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// Statements: the ones that exist and are wrong, and the ones with no entry point.
/// </summary>
public class StatementAdversaryTests
{
    /// <summary>
    /// A <c>case</c> label built from a string is code, consistently with every other entry point.
    /// A literal label asks for a literal.
    /// </summary>
    /// <remarks>
    /// The placeholder this replaces treated <c>case abc:</c> as the defect. It is the contract:
    /// <c>AddCase(name)</c> is how a generated switch reaches a <c>const</c> or an enum member by
    /// name, which is most of what generated switches do.
    /// </remarks>
    [Fact]
    public void SwitchCaseOnAStringValue()
    {
        var method = new MethodDefinition("M");

        method.AddCode("var x = \"a\";");
        method.Switch(CodeOutputComponent.Get("x"))
            .AddCase(SyntaxHelpers.QuoteString("abc"))
            .AddCode("break;");

        var emitted = Emit.Component(method);

        Assert.Contains("case \"abc\":", emitted);
        RoslynAssert.MemberCompiles(emitted);
    }

    /// <summary>
    /// §7 records that <c>Catch(Type, name, when)</c> drops its filter. Here it is as a compile
    /// question: with the filter gone, a second catch for the same type is unreachable - CS0160.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP (§7 'Catch(Type, name, when)'): the when argument is accepted and never forwarded, so the filter disappears and two filtered catches for one type collide - CS0160")]
    public void CatchWhenFilterIsForwarded()
    {
        var block = new TryCatchBlock();

        block.AddCode("Work();");
        block.Catch(typeof(InvalidOperationException), "e",
            CodeOutputComponent.Get("e.Message != null")).AddCode("A();");
        block.Catch(typeof(InvalidOperationException), "e2",
            CodeOutputComponent.Get("e2.Message == null")).AddCode("B();");

        var method = new MethodDefinition("M");

        method.Add(block);

        RoslynAssert.MemberCompiles(
            Emit.Component(method),
            preamble: "",
            languageVersion: RoslynAssert.MaxLanguageVersion);
    }

    /// <summary>
    /// §7 records <c>ForDefinition</c> as an empty stub. It is also unreachable - nothing on
    /// <c>BaseBlockDefinition</c> creates one - so a caller who finds the type and constructs it by
    /// hand gets a component that writes nothing at all, silently swallowing its own body.
    /// </summary>
    [Fact]
    public void ForLoopWritesItsBody()
    {
        var loop = new ForDefinition();

        loop.AddCode("Work();");

        Assert.Contains("Work();", Emit.Component(loop));
    }

    /// <summary>
    /// <c>Continue()</c> exists on <see cref="BaseBlockDefinition"/>, alongside <c>Break()</c>.
    /// The placeholder this replaces said it did not.
    /// </summary>
    [Fact]
    public void ContinueStatement()
    {
        var method = new MethodDefinition("M");
        var loop = method.For("i", 0, 10);

        loop.Continue();

        var emitted = Emit.Component(method);

        Assert.Contains("continue;", emitted);
        RoslynAssert.MemberCompiles(emitted);
    }

    // ---- statements that do work, kept as guards ----

    [Fact]
    public void IfElseIfElseCompiles()
    {
        var method = new MethodDefinition("M");

        var block = method.If("a");

        block.AddCode("A();");
        block.ElseIf("b").AddCode("B();");
        block.Else().AddCode("C();");

        RoslynAssert.MemberCompiles(
            "bool a, b;\n    void A() { }\n    void B() { }\n    void C() { }\n" +
            Emit.Component(method));
    }

    [Fact]
    public void TryCatchFinallyCompiles()
    {
        var block = new TryCatchBlock();

        block.AddCode("Work();");
        block.Catch(typeof(InvalidOperationException), "e").AddCode("Handle(e);");
        block.Finally().AddCode("Done();");

        var method = new MethodDefinition("M");

        method.Add(block);

        RoslynAssert.MemberCompiles(
            Emit.Component(method) +
            "\n    void Work() { }\n    void Handle(Exception e) { }\n    void Done() { }");
    }

    [Fact]
    public void SwitchOnAnIntCompiles()
    {
        var method = new MethodDefinition("M");

        method.AddCode("var x = 1;");

        var block = method.Switch(CodeOutputComponent.Get("x"));

        block.AddCase(1).AddCode("break;");
        block.AddCase(2).AddCode("break;");
        block.AddDefault().AddCode("break;");

        RoslynAssert.MemberCompiles(Emit.Component(method));
    }

    [Fact]
    public void ForEachCompiles()
    {
        var method = new MethodDefinition("M");

        method.AddCode("var items = new int[0];");
        method.ForEach("item", CodeOutputComponent.Get("items")).AddCode("var y = item;");

        RoslynAssert.MemberCompiles(Emit.Component(method));
    }

    [Fact]
    public void WhileWithBreakCompiles()
    {
        var method = new MethodDefinition("M");

        var loop = method.While(CodeOutputComponent.Get("true"));

        loop.Break();

        RoslynAssert.MemberCompiles(Emit.Component(method));
    }

    [Fact]
    public void ThrowCompiles()
    {
        var method = new MethodDefinition("M");

        method.Throw(typeof(InvalidOperationException), QuoteString("bad"));

        RoslynAssert.MemberCompiles(Emit.Component(method));
    }

    [Fact]
    public void YieldReturnCompiles()
    {
        var method = new MethodDefinition("M")
            .SetReturnType(TypeDefinition.IEnumerable(typeof(int)));

        method.Add(new IndentedStatementComponent(YieldReturn(CodeOutputComponent.Get("1"))));

        RoslynAssert.MemberCompiles(Emit.Component(method));
    }

    [Fact]
    public void LocalDeclarationAndAssignmentCompile()
    {
        var method = new MethodDefinition("M");

        method.Assign(CodeOutputComponent.Get("1")).ToVar("x");
        method.Assign(CodeOutputComponent.Get("2")).ToLocal(TypeDefinition.Get(typeof(int)), "y");
        method.Assign(CodeOutputComponent.Get("3")).To("x");

        RoslynAssert.MemberCompiles(Emit.Component(method));
    }
}
