using System;
using Xunit;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// Keeps <c>docs/api-gaps.md</c> honest by running the entries it makes claims about.
/// </summary>
/// <remarks>
/// <para>
/// That file has now gone stale twice. Its own preamble explains the first time - 93
/// <c>[Fact(Skip = "ADVERSARY GAP: …")]</c> placeholders, 21 of which described features that
/// already existed - and diagnoses why that shape cannot work: un-skipping one always fails,
/// whether or not the gap is still real, so nothing ever forces a placeholder to be revisited when
/// its feature ships. Moving the list into prose fixed the failure mode by removing the test, which
/// is why it went stale again.
/// </para>
/// <para>
/// These tests are the shape the file asks for and does not have. Each pins what the documentation
/// currently claims, and fails when reality moves <em>either way</em>:
/// </para>
/// <list type="bullet">
/// <item><description>a construct documented as <b>closed</b> that stops emitting fails here;</description></item>
/// <item><description>a construct documented as <b>still open</b> that starts working <b>also</b>
/// fails here - which is the direction prose can never catch, and the one that made the file
/// misleading rather than merely incomplete.</description></item>
/// </list>
/// <para>
/// A failure is not necessarily a defect. It means the documentation and the library disagree, and
/// the fix may be to either.
/// </para>
/// </remarks>
public class ApiGapLedgerTests
{
    private static string Render(CSharpFileDefinition file) =>
        Emit.File(file, new OutputContextOptions { TypeOutputMode = TypeOutputMode.ShortName });

    private static MethodDefinition MoneyMember(out CSharpFileDefinition file, string name)
    {
        file = new CSharpFileDefinition("Probe.Ledger");

        var money = file.AddClass("Money");
        money.Modifiers = ComponentModifier.Public;

        var member = money.AddMethod(name);
        member.Modifiers = ComponentModifier.Public | ComponentModifier.Static;

        return member;
    }

    /// <summary>
    /// `Operator Declarations` — documented as **partly wrong**: reachable, by an unobvious route.
    /// </summary>
    /// <remarks>
    /// <c>MethodDefinition</c> writes its name where the operator keyword and symbol go, so naming
    /// a method <c>operator +</c> produces a real operator declaration. Nothing validates the name,
    /// which is why the entry stays in the file rather than being struck through - but the entry
    /// used to say the construct could not be written, and it can.
    /// </remarks>
    [Fact]
    public void OperatorDeclarationIsReachableByNamingTheMethod()
    {
        var money = TypeDefinition.Get("Probe.Ledger", "Money");

        var op = MoneyMember(out var file, "operator +");
        op.SetReturnType(money);
        op.AddParameter(money, "a");
        op.AddParameter(money, "b");
        op.Return(CodeOutputComponent.Get("a"));

        var output = Render(file);

        Assert.Contains("public static Money operator +(Money a, Money b)", output);

        RoslynAssert.Compiles(output);
    }

    /// <summary>
    /// `Conversion Operators` — documented as **still open**.
    /// </summary>
    /// <remarks>
    /// The operator trick does not extend here: a conversion operator declares no return type and
    /// <c>MethodDefinition</c> always writes one, so the <c>void</c> lands between <c>static</c>
    /// and <c>implicit</c>.
    /// <para>
    /// <b>When this test fails, the gap has closed.</b> Strike the entry in
    /// <c>docs/api-gaps.md</c> and replace this with a test that asserts the working output.
    /// </para>
    /// </remarks>
    [Fact]
    public void ConversionOperatorStillWritesAReturnType()
    {
        var money = TypeDefinition.Get("Probe.Ledger", "Money");

        var conversion = MoneyMember(out var file, "implicit operator int");
        conversion.AddParameter(money, "m");
        conversion.Return(CodeOutputComponent.Get("0"));

        var output = Render(file);

        Assert.Contains("public static void implicit operator int(Money m)", output);

        // And the reason it is a gap rather than a cosmetic complaint.
        Assert.NotEmpty(RoslynAssert.Errors(output));
    }

    /// <summary>
    /// `Destructors` — documented as **still open**.
    /// </summary>
    /// <remarks>
    /// Same cause as the conversion operator. <b>When this test fails, the gap has closed.</b>
    /// </remarks>
    [Fact]
    public void DestructorStillWritesAReturnType()
    {
        var file = new CSharpFileDefinition("Probe.Ledger");

        var host = file.AddClass("Host");
        host.Modifiers = ComponentModifier.Public;

        var destructor = host.AddMethod("~Host");
        destructor.Modifiers = ComponentModifier.NoAccessibility;

        var output = Render(file);

        Assert.Contains("void ~Host()", output);
        Assert.NotEmpty(RoslynAssert.Errors(output));
    }

    /// <summary>
    /// `Generic Interfaces` — documented as **still open**.
    /// </summary>
    /// <remarks>
    /// <c>InterfaceDefinition</c> has no generic-parameter list, so the type parameters simply do
    /// not appear. <b>When this test fails, the gap has closed.</b>
    /// </remarks>
    [Fact]
    public void InterfaceStillCannotDeclareTypeParameters()
    {
        var file = new CSharpFileDefinition("Probe.Ledger");

        var contract = file.AddInterface("IRepo");
        contract.Modifiers = ComponentModifier.Public;

        var output = Render(file);

        Assert.Contains("interface IRepo", output);
        Assert.DoesNotContain("IRepo<", output);
    }

    /// <summary>
    /// `Volatile Fields` — documented as **closed**.
    /// </summary>
    [Fact]
    public void VolatileGapIsClosed()
    {
        var file = new CSharpFileDefinition("Probe.Ledger");

        var host = file.AddClass("Host");
        host.Modifiers = ComponentModifier.Public;

        var field = host.AddField(typeof(int), "_state");
        field.Modifiers = ComponentModifier.Private | ComponentModifier.Volatile;

        Assert.Contains("private volatile int _state;", Render(file));
    }

    /// <summary>
    /// `Base Class Constraint Ordering` — documented as **closed**.
    /// </summary>
    [Fact]
    public void BaseClassConstraintOrderingGapIsClosed()
    {
        var file = new CSharpFileDefinition("Probe.Ledger");

        var host = file.AddClass("Host");
        host.Modifiers = ComponentModifier.Public;

        var method = host.AddMethod("Go");
        method.Modifiers = ComponentModifier.Public;
        method.SetReturnType(typeof(void));
        method.AddGenericParameter(TypeDefinition.Get("", "T"));
        method.AddConstraint("T")
            .Implements(TypeDefinition.Get(typeof(IDisposable)))
            .Implements(TypeDefinition.Get(typeof(System.IO.Stream)));

        var output = Render(file);

        Assert.True(
            output.IndexOf("Stream", StringComparison.Ordinal) <
            output.IndexOf("IDisposable", StringComparison.Ordinal));

        RoslynAssert.Compiles(output);
    }
}
