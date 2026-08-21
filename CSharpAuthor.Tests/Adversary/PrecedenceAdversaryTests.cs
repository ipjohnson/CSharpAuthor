using Xunit;
using static CSharpAuthor.SyntaxHelpers;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// Places where the parenthesisation changes what the expression means.
/// </summary>
/// <remarks>
/// <c>LogicStatement</c> parenthesises everything it writes, which is over-cautious and always
/// correct. The interesting cases are the components that do not: a cast, and a member access built
/// on top of one.
/// </remarks>
public class PrecedenceAdversaryTests
{
    private const string Shapes = @"
using Probe;
namespace Probe
{
    public class Animal { public string Name = ""a""; }
    public class Dog : Animal { public string Breed = ""b""; }
    public class Box { public Animal Value = new Dog(); }
}
";

    /// <summary>
    /// The worst expression defect found. <c>StaticCast</c> writes <c>(T)</c> and then the operand,
    /// with nothing holding them together, so composing a member access on top of it produces
    /// <c>(Dog)box.Value</c> - which C# parses as <c>(Dog)(box.Value)</c>. The cast has jumped over
    /// the member access to the far side of it.
    /// </summary>
    /// <remarks>
    /// It compiles whenever the conversion is legal at the outer position, so nothing reports it.
    /// Here the caller asked for the <c>Breed</c> of a cast value and got a cast of <c>Value</c> -
    /// two different expressions, one of which happens to be well-typed.
    /// </remarks>
    [Fact(Skip = "ADVERSARY GAP: Property(StaticCast(T, x), \"M\") emits (T)x.M, which parses as (T)(x.M) - the cast binds looser than the member access and silently applies to the wrong operand")]
    public void CastThenMemberAccess()
    {
        var expression = Property(
            StaticCast(TypeDefinition.Get("Probe", "Dog"), "animal"),
            "Breed");

        Assert.Equal("((Dog)animal).Breed", Emit.Component(expression));
    }

    /// <summary>
    /// The same defect asked of the compiler. <c>animal</c> is an <c>Animal</c>, which has no
    /// <c>Breed</c>, so <c>(Dog)animal.Breed</c> cannot bind - the member access happens first, on
    /// the uncast value.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: emits (Dog)animal.Breed - CS1061, because the member access binds before the cast")]
    public void CastThenMemberAccess_Compiles()
    {
        var expression = Property(
            StaticCast(TypeDefinition.Get("Probe", "Dog"), "animal"),
            "Breed");

        RoslynAssert.StatementCompiles(
            "Probe.Animal animal = new Probe.Dog();\nvar r = " + Emit.Component(expression) + ";",
            preamble: Shapes);
    }

    /// <summary>
    /// Invoking on a cast has the same shape and the same defect.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: Invoke on a cast emits (T)x.M(), which parses as (T)(x.M()) - the cast applies to the result of the call rather than to its receiver")]
    public void CastThenInvoke()
    {
        var expression = StaticCast(TypeDefinition.Get("Probe", "Dog"), "animal")
            .Invoke("ToString");

        Assert.Equal("((Dog)animal).ToString()", Emit.Component(expression));
    }

    /// <summary>
    /// A cast of an expression already carries parentheses from <c>LogicStatement</c>, so this one
    /// is right. Unskipped as a guard, so a fix that adds parentheses everywhere does not double
    /// them here.
    /// </summary>
    [Fact]
    public void CastOfASum()
    {
        Assert.Equal("(int)(1 + 2)", Emit.Component(StaticCast(typeof(int), Add(1, 2))));
    }

    [Fact]
    public void MultiplyInsideAddIsParenthesised()
    {
        RoslynAssert.ExpressionCompiles(Emit.Component(Add(1, Multiply(2, 3))));

        Assert.Equal("(1 + (2 * 3))", Emit.Component(Add(1, Multiply(2, 3))));
    }

    [Fact]
    public void AddInsideMultiplyIsParenthesised()
    {
        Assert.Equal("((1 + 2) * 3)", Emit.Component(Multiply(Add(1, 2), 3)));
    }

    [Fact]
    public void OrInsideAndIsParenthesised()
    {
        Assert.Equal("((a || b) && c)", Emit.Component(And(Or("a", "b"), "c")));
    }

    [Fact]
    public void NullCoalesceInsideAddIsParenthesised()
    {
        Assert.Equal("((a ?? b) + c)", Emit.Component(Add(NullCoalesce("a", "b"), "c")));
    }

    /// <summary>
    /// <c>is</c> binds tighter than <c>&amp;&amp;</c>, so this reads correctly even unparenthesised.
    /// </summary>
    [Fact]
    public void IsInsideAnd()
    {
        RoslynAssert.StatementCompiles(
            "object x = null; var y = true;\nif " +
            Emit.Component(And(Is(CodeOutputComponent.Get("x"), TypeDefinition.Get("Probe", "Dog")), "y")) +
            " { }",
            preamble: Shapes);
    }

    /// <summary>
    /// A conditional written as an <c>if</c> drops its outer parentheses, because the block writes
    /// its own. Guard against a precedence fix reinstating them and producing <c>if ((a &amp;&amp; b))</c>.
    /// </summary>
    [Fact]
    public void IfConditionIsNotDoubleParenthesised()
    {
        var method = new MethodDefinition("M");

        method.If(And("a", "b")).AddCode("return;");

        Assert.Contains("if (a && b)", Emit.Component(method));
    }

    /// <summary>
    /// Indexing a cast has the same shape: <c>(Foo)x[0]</c> parses as <c>(Foo)(x[0])</c>, so the
    /// cast applies to the element rather than to the thing being indexed.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: Index on a cast emits (T)x[0], which parses as (T)(x[0]) - the cast lands on the element rather than on the receiver")]
    public void CastThenIndex()
    {
        var expression = StaticCast(TypeDefinition.Get("Probe", "Dog"), "animals").Index(0);

        Assert.Equal("((Dog)animals)[0]", Emit.Component(expression));
    }

    /// <summary>
    /// The same family again, with <c>await</c>. A caller that composes a member access onto an
    /// awaited call means <c>(await GetAsync()).Length</c>; what is written is
    /// <c>await GetAsync().Length</c>, which awaits the member rather than the call.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: PrefixOutputComponent adds no parentheses, so Property(Await(x), \"Y\") emits 'await x.Y' - the await applies to the member access, not to x")]
    public void AwaitThenMemberAccess()
    {
        var expression = Property(Await(Invoke("GetAsync")), "Length");

        Assert.Equal("(await GetAsync()).Length", Emit.Component(expression));
    }

    /// <summary>
    /// <c>++</c> on a composed expression, where the wrong reading is at least loud: <c>(a + b)++</c>
    /// is CS1059. The interesting part is that nothing distinguishes it from the correct
    /// <c>x.Count++</c> at the point the component is built.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: PostfixOutputComponent adds no parentheses and applies no precedence rule, so Increment of any composed expression is written as if it were a variable - (a + b)++, CS1059")]
    public void IncrementOfAnExpressionIsRejectedOrParenthesised()
    {
        RoslynAssert.StatementCompiles("int a = 1, b = 2;\nvar r = " +
            Emit.Component(Increment(Add("a", "b"))) + ";");
    }

    /// <summary>
    /// Postfix on a member access is correct, and is the case consumers actually use. Guard.
    /// </summary>
    [Fact]
    public void IncrementOfAMemberAccess()
    {
        Assert.Equal("x.Count++", Emit.Component(Increment(Property(CodeOutputComponent.Get("x"), "Count"))));
    }

    /// <summary>
    /// <c>?.</c> and <c>?[]</c> compose correctly out of <c>Question</c>. Guards, because a
    /// parenthesisation fix could easily turn these into <c>(x?).Y</c>.
    /// </summary>
    [Fact]
    public void ConditionalAccessComposes()
    {
        Assert.Equal("x?.Y", Emit.Component(Property(Question(CodeOutputComponent.Get("x")), "Y")));
        Assert.Equal("x?[0]", Emit.Component(Question(CodeOutputComponent.Get("x")).Index(0)));
        Assert.Equal("x!.Y", Emit.Component(Property(Bang(CodeOutputComponent.Get("x")), "Y")));
    }

    /// <summary>
    /// A member access on a <c>new</c> needs no parentheses and gets none. Guard.
    /// </summary>
    [Fact]
    public void MemberAccessOnNew()
    {
        Assert.Equal("new Foo().X",
            Emit.Component(Property(New(TypeDefinition.Get("Probe", "Foo")), "X")));
    }

    /// <summary>
    /// <c>await</c> binds tighter than a member access, so <c>await x.M()</c> awaits the call. That
    /// is what a caller composing <c>Await(...)</c> onto an invocation means, and it is what is
    /// written. Guard.
    /// </summary>
    [Fact]
    public void AwaitOfAnInvocation()
    {
        var expression = Await(Invoke("GetAsync"));

        Assert.Equal("await GetAsync()", Emit.Component(expression));
    }
}
