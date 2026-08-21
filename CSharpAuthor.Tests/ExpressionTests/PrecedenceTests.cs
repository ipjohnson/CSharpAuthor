using CSharpAuthor.Expressions;
using Xunit;

namespace CSharpAuthor.Tests.ExpressionTests;

/// <summary>
/// One test per trap. The invariant under test is that the emitted text re-parses to the
/// tree it was built from — a missing bracket here does not throw and does not fail to
/// compile, it silently computes something else.
/// </summary>
/// <remarks>
/// The expectations were checked against the compiler rather than recalled. The probes
/// that settled the surprising ones are recorded in the comments.
/// </remarks>
public class PrecedenceTests
{
    private static readonly Ex A = Ex.Id("a");
    private static readonly Ex B = Ex.Id("b");
    private static readonly Ex C = Ex.Id("c");
    private static readonly Ex D = Ex.Id("d");
    private static readonly Ex E = Ex.Id("e");

    // -------------------------------------------------------------------------------
    // Left associativity: the right operand of a left-associative operator is the one
    // that has to be bracketed.
    // -------------------------------------------------------------------------------

    [Fact]
    public void SubtractionIsLeftAssociativeSoTheLeftNestingNeedsNoBrackets()
    {
        ExAssert.Emits("a - b - c", Ex.Subtract(Ex.Subtract(A, B), C));
    }

    [Fact]
    public void SubtractionOnTheRightKeepsItsBrackets()
    {
        // `a - b - c` is `(a - b) - c`. Dropping these brackets changes the value.
        ExAssert.Emits("a - (b - c)", Ex.Subtract(A, Ex.Subtract(B, C)));
    }

    [Fact]
    public void AdditionOnTheRightOfAdditionKeepsItsBracketsToo()
    {
        // Same precedence, different operator, same rule — the tree is preserved even
        // though the arithmetic would survive.
        ExAssert.Emits("a + (b + c)", Ex.Add(A, Ex.Add(B, C)));
    }

    [Fact]
    public void DivisionOnTheRightOfMultiplicationKeepsItsBrackets()
    {
        ExAssert.Emits("a * (b / c)", Ex.Multiply(A, Ex.Divide(B, C)));
    }

    [Fact]
    public void TighterOperatorsNestWithoutBrackets()
    {
        ExAssert.Emits("a + b * c", Ex.Add(A, Ex.Multiply(B, C)));
    }

    [Fact]
    public void LooserOperatorsUnderTighterOnesAreBracketed()
    {
        ExAssert.Emits("(a + b) * c", Ex.Multiply(Ex.Add(A, B), C));
    }

    [Fact]
    public void ConditionalAndBindsTighterThanConditionalOr()
    {
        ExAssert.Emits("a && b || c", Ex.OrElse(Ex.AndAlso(A, B), C));
    }

    [Fact]
    public void ConditionalOrUnderConditionalAndIsBracketed()
    {
        ExAssert.Emits("a && (b || c)", Ex.AndAlso(A, Ex.OrElse(B, C)));
    }

    [Fact]
    public void RelationalBindsTighterThanEquality()
    {
        ExAssert.Emits("a < b == c < d", Ex.Equal(Ex.LessThan(A, B), Ex.LessThan(C, D)));
    }

    [Fact]
    public void AdditiveBindsTighterThanShiftInBothDirections()
    {
        ExAssert.Emits("a + b << c", Ex.ShiftLeft(Ex.Add(A, B), C));
        ExAssert.Emits("a << b + c", Ex.ShiftLeft(A, Ex.Add(B, C)));
    }

    // -------------------------------------------------------------------------------
    // Right associativity: the mirror image.
    // -------------------------------------------------------------------------------

    [Fact]
    public void NullCoalescingIsRightAssociativeSoTheRightNestingIsBare()
    {
        // Verified: `c1 ?? c2 ?? c3` yields c3 when the first two are null.
        ExAssert.Emits("a ?? b ?? c", Ex.Coalesce(A, Ex.Coalesce(B, C)));
    }

    [Fact]
    public void NullCoalescingOnTheLeftKeepsItsBrackets()
    {
        ExAssert.Emits("(a ?? b) ?? c", Ex.Coalesce(Ex.Coalesce(A, B), C));
    }

    [Fact]
    public void ConditionalIsRightAssociativeSoTheElseBranchIsBare()
    {
        ExAssert.Emits("a ? b : c ? d : e", Ex.Conditional(A, B, Ex.Conditional(C, D, E)));
    }

    [Fact]
    public void ConditionalInTheThenBranchIsBareToo()
    {
        // Verified: `p ? q ? 1 : 2 : 3` is `p ? (q ? 1 : 2) : 3` — the colon matches
        // inwards, so no brackets are needed to preserve the tree.
        ExAssert.Emits("a ? b ? c : d : e", Ex.Conditional(A, Ex.Conditional(B, C, D), E));
    }

    [Fact]
    public void ConditionalInTheConditionPositionIsBracketed()
    {
        ExAssert.Emits("(a ? b : c) ? d : e", Ex.Conditional(Ex.Conditional(A, B, C), D, E));
    }

    [Fact]
    public void AssignmentIsRightAssociative()
    {
        ExAssert.Emits("a = b = c", Ex.Assign(A, Ex.Assign(B, C)));
    }

    [Fact]
    public void CompoundAssignmentTakesAFullExpressionOnTheRight()
    {
        ExAssert.Emits("a += b + c", Ex.AssignOperator("+=", A, Ex.Add(B, C)));
    }

    // -------------------------------------------------------------------------------
    // Null-coalescing against the conditional operator.
    // -------------------------------------------------------------------------------

    [Fact]
    public void CoalescingBindsTighterThanTheConditionalCondition()
    {
        // Verified: `bn ?? bt ? 1 : 2` uses `(bn ?? bt)` as the condition.
        ExAssert.Emits("a ?? b ? c : d", Ex.Conditional(Ex.Coalesce(A, B), C, D));
    }

    [Fact]
    public void AConditionalOnTheRightOfCoalescingIsBracketed()
    {
        // The right side of `??` is a null_coalescing_expression, which excludes `?:`.
        ExAssert.Emits("a ?? (b ? c : d)", Ex.Coalesce(A, Ex.Conditional(B, C, D)));
    }

    // -------------------------------------------------------------------------------
    // Unary against binary, and the one lexical hazard precedence cannot express.
    // -------------------------------------------------------------------------------

    [Fact]
    public void NegatingANegationBracketsRatherThanRelyingOnASpace()
    {
        // `--a` is a pre-decrement, not a double negation. A space would also work, but a
        // bracket cannot be lost to a later reformat.
        ExAssert.Emits("-(-a)", Ex.Negate(Ex.Negate(A)));
    }

    [Fact]
    public void NegatingAPreDecrementBracketsForTheSameReason()
    {
        ExAssert.Emits("-(--a)", Ex.Negate(Ex.PreDecrement(A)));
    }

    [Fact]
    public void DoubleNegationOfABooleanNeedsNothing()
    {
        // `!!a` has no lexical collision, so it stays bare.
        ExAssert.Emits("!!a", Ex.Not(Ex.Not(A)));
    }

    [Fact]
    public void UnaryMinusAsTheRightOperandOfSubtractionIsBare()
    {
        // The binary operator writes its own spaces, so `a - -b` cannot re-lex as `--`.
        ExAssert.Emits("a - -b", Ex.Subtract(A, Ex.Negate(B)));
    }

    [Fact]
    public void NotAppliedToAnIsTestIsBracketed()
    {
        // Verified: `!o is string` parses as `(!o) is string` and fails to compile.
        var expression = Ex.Not(Ex.Is(A, ExAssert.Type("B")));

        ExAssert.Emits("!(a is B)", expression);
    }

    [Fact]
    public void NotAppliedToASumIsBracketed()
    {
        ExAssert.Emits("-(a + b)", Ex.Negate(Ex.Add(A, B)));
    }

    [Fact]
    public void UnaryAppliedToAPrimaryIsBare()
    {
        ExAssert.Emits("-a.b", Ex.Negate(A.Dot("b")));
    }

    [Fact]
    public void AwaitTakesAUnaryOperandAndBracketsAnythingLooser()
    {
        ExAssert.Emits("await a.b()", Ex.Await(A.Call("b")));
        ExAssert.Emits("await (a ?? b)", Ex.Await(Ex.Coalesce(A, B)));
    }

    // -------------------------------------------------------------------------------
    // Casts. The operand is a unary expression, which is looser than it looks.
    // -------------------------------------------------------------------------------

    [Fact]
    public void CastOfAUnaryOperandNeedsNoBrackets()
    {
        // Verified to compile: `(int)-n`.
        ExAssert.Emits("(int)-x", Ex.Cast(TypeDefinition.Get(typeof(int)), Ex.Negate(Ex.Id("x"))));
    }

    [Fact]
    public void CastOfASumBracketsTheSum()
    {
        ExAssert.Emits("(int)(a + b)", Ex.Cast(TypeDefinition.Get(typeof(int)), Ex.Add(A, B)));
    }

    [Fact]
    public void InvokingTheResultOfACastBracketsTheCast()
    {
        var cast = Ex.Cast(ExAssert.Type("A"), B);

        ExAssert.Emits("((A)b)(c)", cast.Invoke(C));
    }

    [Fact]
    public void CallingAnIdentifierNeverProducesTheCastShapedAmbiguity()
    {
        // `(a)(b)` would read as a cast to a type named `a`. An identifier target is
        // already primary, so the brackets are never added and the ambiguity is
        // unreachable.
        ExAssert.Emits("a(b)", A.Invoke(B));
    }

    [Fact]
    public void CastOfACallIsBare()
    {
        ExAssert.Emits("(A)b(c)", Ex.Cast(ExAssert.Type("A"), B.Invoke(C)));
    }

    // -------------------------------------------------------------------------------
    // `is` and `as` against the logical operators.
    // -------------------------------------------------------------------------------

    [Fact]
    public void IsBindsTighterThanConditionalAndSoNeedsNoBrackets()
    {
        // Verified to compile: `o is string && n > 0`.
        ExAssert.Emits("a is B && c", Ex.AndAlso(Ex.Is(A, ExAssert.Type("B")), C));
    }

    [Fact]
    public void AConditionalAndUnderIsIsBracketed()
    {
        ExAssert.Emits("(a && b) is B", Ex.Is(Ex.AndAlso(A, B), ExAssert.Type("B")));
    }

    [Fact]
    public void IsTakesASumOnTheLeftWithoutBrackets()
    {
        ExAssert.Emits("a + b is B", Ex.Is(Ex.Add(A, B), ExAssert.Type("B")));
    }

    [Fact]
    public void NullForgivingOnACastLikeConversionBracketsTheConversion()
    {
        // Verified to compile: `(obj as string)!.Length`.
        var expression = Ex.SuppressNullWarning(Ex.As(A, ExAssert.Type("B"))).Dot("C");

        ExAssert.Emits("(a as B)!.C", expression);
    }

    [Fact]
    public void NullForgivingOnAPrimaryIsBare()
    {
        ExAssert.Emits("a.b!", Ex.SuppressNullWarning(A.Dot("b")));
    }

    // -------------------------------------------------------------------------------
    // Null-conditional chains. These are not a precedence nicety: `node?.B.C` yields null
    // when `node` is null, and `(node?.B).C` throws. Verified, not assumed.
    // -------------------------------------------------------------------------------

    [Fact]
    public void AChainBuiltAsOneChainStaysOneChain()
    {
        ExAssert.Emits("a?.b.c", A.NullAccess(root => root.Dot("b").Dot("c")));
    }

    [Fact]
    public void MemberAccessOnAFinishedChainIsBracketed()
    {
        ExAssert.Emits("(a?.b).c", A.NullDot("b").Dot("c"));
    }

    [Fact]
    public void InvocationOnAFinishedChainIsBracketed()
    {
        ExAssert.Emits("(a?.b)(c)", A.NullDot("b").Invoke(C));
    }

    [Fact]
    public void NullConditionalElementAccess()
    {
        ExAssert.Emits("a?[0]", A.NullIndex(Ex.Int(0)));
    }

    [Fact]
    public void AChainAsAnOperandOfABinaryOperatorNeedsNothing()
    {
        ExAssert.Emits("a?.b + c", Ex.Add(A.NullDot("b"), C));
    }

    [Fact]
    public void NullConditionalCallInsideAChain()
    {
        ExAssert.Emits("a?.b(c).d", A.NullAccess(root => root.Call("b", C).Dot("d")));
    }

    // -------------------------------------------------------------------------------
    // Switch and with. Both bind TIGHTER than multiplicative — verified by running
    // `2 * x switch { 1 => 10, 2 => 30, _ => 99 }`, which is 20, not 30.
    // -------------------------------------------------------------------------------

    [Fact]
    public void ASumGoverningASwitchExpressionIsBracketed()
    {
        var expression = Ex.SwitchInline(
            Ex.Add(A, B),
            Ex.Arm(Pat.Constant(Ex.Int(1)), C),
            Ex.Arm(Pat.Discard, D));

        ExAssert.Emits("(a + b) switch { 1 => c, _ => d }", expression);
    }

    [Fact]
    public void ASwitchExpressionAsTheRightOperandOfMultiplicationIsBare()
    {
        var expression = Ex.Multiply(
            A,
            Ex.SwitchInline(B, Ex.Arm(Pat.Discard, C)));

        ExAssert.Emits("a * b switch { _ => c }", expression);
    }

    [Fact]
    public void ASumGoverningAWithExpressionIsBracketed()
    {
        var expression = Ex.With(Ex.Add(A, B), Ex.Assign(Ex.Id("X"), Ex.Int(1)));

        ExAssert.Emits("(a + b) with { X = 1 }", expression);
    }

    [Fact]
    public void WithExpressionsChainWithoutBrackets()
    {
        // Verified to compile and evaluate: `rec with { X = 3 } with { Y = 4 }`.
        var expression = Ex.With(
            Ex.With(A, Ex.Assign(Ex.Id("X"), Ex.Int(1))),
            Ex.Assign(Ex.Id("Y"), Ex.Int(2)));

        ExAssert.Emits("a with { X = 1 } with { Y = 2 }", expression);
    }

    // -------------------------------------------------------------------------------
    // Ranges. `..` binds tighter than `+` — verified: `-1 + 2..^2` is a compile error
    // because it means `-1 + (2..^2)`.
    // -------------------------------------------------------------------------------

    [Fact]
    public void ASumInsideARangeIsBracketed()
    {
        ExAssert.Emits("(a + b)..c", Ex.Range(Ex.Add(A, B), C));
    }

    [Fact]
    public void UnaryEndpointsInARangeAreBare()
    {
        ExAssert.Emits("-1..^2", Ex.Range(Ex.Negate(Ex.Int(1)), Ex.FromEnd(Ex.Int(2))));
    }

    [Fact]
    public void OpenEndedRanges()
    {
        ExAssert.Emits("a..", Ex.Range(A, null));
        ExAssert.Emits("..b", Ex.Range(null, B));
        ExAssert.Emits("..", Ex.Range(null, null));
    }

    [Fact]
    public void RangeInsideAnIndexer()
    {
        ExAssert.Emits("a[1..^1]", A.Index(Ex.Range(Ex.Int(1), Ex.FromEnd(Ex.Int(1)))));
    }

    // -------------------------------------------------------------------------------
    // Lambdas as a precedence context.
    // -------------------------------------------------------------------------------

    [Fact]
    public void ALambdaBodyIsAFullExpressionAndNeedsNoBrackets()
    {
        ExAssert.Emits("x => a + b", Ex.Lambda("x", Ex.Add(A, B)));
    }

    [Fact]
    public void ALambdaBodyMayBeAnotherLambda()
    {
        ExAssert.Emits("x => y => a", Ex.Lambda("x", Ex.Lambda("y", A)));
    }

    [Fact]
    public void ALambdaBodyMayBeAConditional()
    {
        ExAssert.Emits("x => a ? b : c", Ex.Lambda("x", Ex.Conditional(A, B, C)));
    }

    [Fact]
    public void InvokingALambdaBracketsIt()
    {
        // Verified to compile in its cast form: `((Func<int,int>)(y => y + 1))(3)`.
        ExAssert.Emits("(x => a)(b)", Ex.Lambda("x", A).Invoke(B));
    }

    [Fact]
    public void InvokingALambdaThroughACastBracketsBothLayers()
    {
        // The form that actually compiles: a lambda literal has no type of its own, so
        // invoking one means casting it first. Both brackets are load-bearing - the inner
        // pair because a cast takes a unary operand, the outer because an invocation
        // target must be a primary.
        var funcType = TypeDefinition.Get("System", "Func<int, int>");
        var expression = Ex.Cast(funcType, Ex.Lambda("x", Ex.Id("x"))).Invoke(Ex.Int(3));

        ExAssert.Emits("((Func<int, int>)(x => x))(3)", expression);
    }

    [Fact]
    public void ACastOfAnObjectInitializerComposes()
    {
        var expression = Ex.Cast(
            ExAssert.Type("B"),
            Ex.NewWithInitializer(ExAssert.Type("C"), null, Ex.Assign(Ex.Id("X"), Ex.Int(1))));

        ExAssert.Emits("(B)new C { X = 1 }", expression);
    }

    [Fact]
    public void ALambdaPassedAsAnArgumentIsBare()
    {
        ExAssert.Emits("a(x => b)", A.Invoke(Ex.Lambda("x", B)));
    }

    [Fact]
    public void ALambdaAsAnOperandOfABinaryOperatorIsBracketed()
    {
        ExAssert.Emits("(x => b) ?? c", Ex.Coalesce(Ex.Lambda("x", B), C));
    }

    // -------------------------------------------------------------------------------
    // Switch arms as a precedence context.
    // -------------------------------------------------------------------------------

    [Fact]
    public void ASwitchArmResultIsAFullExpression()
    {
        var expression = Ex.SwitchInline(
            A,
            Ex.Arm(Pat.Constant(Ex.Int(1)), Ex.Conditional(B, C, D)),
            Ex.Arm(Pat.Discard, Ex.Lambda("x", E)));

        ExAssert.Emits("a switch { 1 => b ? c : d, _ => x => e }", expression);
    }

    [Fact]
    public void ANestedSwitchExpressionInAnArmIsBare()
    {
        var inner = Ex.SwitchInline(B, Ex.Arm(Pat.Discard, C));
        var outer = Ex.SwitchInline(A, Ex.Arm(Pat.Discard, inner));

        ExAssert.Emits("a switch { _ => b switch { _ => c } }", outer);
    }

    [Fact]
    public void AGuardIsWrittenBeforeTheArrow()
    {
        var expression = Ex.SwitchInline(
            A,
            Ex.Arm(Pat.Declaration(ExAssert.Type("B"), "b"), Ex.GreaterThan(Ex.Id("b"), Ex.Int(0)), C));

        ExAssert.Emits("a switch { B b when b > 0 => c }", expression);
    }

    // -------------------------------------------------------------------------------
    // `throw` in expression position is never bracketed: `a ?? (throw new T())` is
    // CS8115, verified.
    // -------------------------------------------------------------------------------

    [Fact]
    public void ThrowOnTheRightOfCoalescingIsNotBracketed()
    {
        var expression = Ex.Coalesce(A, Ex.Throw(Ex.New(ExAssert.Type("B"))));

        ExAssert.Emits("a ?? throw new B()", expression);
    }

    [Fact]
    public void ThrowInAConditionalBranchIsNotBracketed()
    {
        var expression = Ex.Conditional(A, B, Ex.Throw(Ex.New(ExAssert.Type("C"))));

        ExAssert.Emits("a ? b : throw new C()", expression);
    }

    [Fact]
    public void ThrowAsALambdaBodyIsNotBracketed()
    {
        ExAssert.Emits("x => throw new B()", Ex.Lambda("x", Ex.Throw(Ex.New(ExAssert.Type("B")))));
    }

    // -------------------------------------------------------------------------------
    // Explicit brackets survive.
    // -------------------------------------------------------------------------------

    [Fact]
    public void ExplicitBracketsArePreserved()
    {
        ExAssert.Emits("(a)", Ex.Paren(A));
        ExAssert.Emits("(a).b", Ex.Paren(A).Dot("b"));
    }

    // -------------------------------------------------------------------------------
    // The operator overloads take the same path as the named methods.
    // -------------------------------------------------------------------------------

    [Fact]
    public void OperatorOverloadsBracketExactlyAsTheNamedFormsDo()
    {
        Ex a = "a";
        Ex b = "b";
        Ex c = "c";

        ExAssert.Emits("a && b || c", (a & b) | c);
        ExAssert.Emits("a && (b || c)", a & (b | c));
        ExAssert.Emits("a - (b - c)", a - (b - c));
        ExAssert.Emits("(a + b) * c", (a + b) * c);
        ExAssert.Emits("!(a && b)", !(a & b));
    }

    [Fact]
    public void AFoldedConjunctionNestsToTheLeftAndStaysBare()
    {
        var expression = Ex.All(new[] { Ex.Id("a"), Ex.Id("b"), Ex.Id("c") });

        ExAssert.Emits("a && b && c", expression);
    }
}
