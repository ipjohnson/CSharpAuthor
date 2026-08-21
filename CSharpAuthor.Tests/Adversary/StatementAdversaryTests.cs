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
    /// A <c>case</c> label built from a string is written unquoted, so switching on a string - which
    /// is most of what generated switches do - emits <c>case abc:</c>. The value has become an
    /// identifier reference.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: SwitchBlockDefinition.AddCase routes through CodeOutputComponent.Get, which writes a string as code - 'case abc:' rather than 'case \"abc\":', CS0103")]
    public void SwitchCaseOnAStringValue()
    {
        var method = new MethodDefinition("M");

        method.AddCode("var x = \"a\";");
        method.Switch(CodeOutputComponent.Get("x")).AddCase("abc").AddCode("break;");

        RoslynAssert.MemberCompiles(Emit.Component(method));
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

    [Fact(Skip = "ADVERSARY GAP (§7 'Continue()'): there is no Continue on BaseBlockDefinition, though Break and Return are both there")]
    public void ContinueStatement()
    {
        Assert.True(false, "no API for continue");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - there is no do/while emitter; WhileDefinition writes the pre-test form only")]
    public void DoWhileStatement()
    {
        Assert.True(false, "no API for do/while");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - there is no using statement or using declaration emitter, so generated code cannot dispose anything without writing the block by hand")]
    public void UsingStatementAndDeclaration()
    {
        Assert.True(false, "no API for using statements or using declarations");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - there is no lock emitter")]
    public void LockStatement()
    {
        Assert.True(false, "no API for lock");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - there is no goto emitter, no label emitter, and so no labelled break or continue either")]
    public void GotoAndLabels()
    {
        Assert.True(false, "no API for goto, labels, or labelled break/continue");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - YieldReturn exists but there is no 'yield break', so an iterator cannot terminate early")]
    public void YieldBreak()
    {
        Assert.True(false, "no API for yield break");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - there is no local function emitter")]
    public void LocalFunctions()
    {
        Assert.True(false, "no API for local functions");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - there is no checked or unchecked emitter, in statement or expression position")]
    public void CheckedAndUnchecked()
    {
        Assert.True(false, "no API for checked / unchecked");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - there is no 'throw' expression (only ThrowNewExceptionStatement, which is a statement), and no rethrow: a bare 'throw;' inside a catch cannot be written except as a raw string")]
    public void ThrowExpressionAndRethrow()
    {
        Assert.True(false, "no API for throw expressions or bare rethrow");
    }

    /// <summary>
    /// <c>ForEachDefinition</c> hard-codes <c>var</c>, so the element type cannot be stated - which
    /// matters when the sequence is <c>IEnumerable</c> rather than <c>IEnumerable&lt;T&gt;</c> and
    /// <c>var</c> would infer <c>object</c>. There is no <c>await foreach</c> either.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: ForEachDefinition writes 'foreach(var x in ...)' with the type fixed as var, so a non-generic sequence cannot be iterated as its element type; and there is no await foreach")]
    public void ForEachWithAnExplicitElementType()
    {
        Assert.True(false, "no API for a typed foreach or await foreach");
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
