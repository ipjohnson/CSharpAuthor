using System.Linq;
using Xunit;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// Proves the adversary's own instrument works.
/// </summary>
/// <remarks>
/// Every other test in this folder is worth exactly as much as <see cref="RoslynAssert"/> is. A
/// harness that accepted everything would turn the whole suite green and prove nothing, and that
/// failure mode is invisible - all the tests pass. So the harness is asked to accept code that
/// compiles and to reject code that does not, before it is trusted with anything else.
/// </remarks>
public class RoslynAssertSelfTests
{
    [Fact]
    public void AcceptsValidCode()
    {
        RoslynAssert.Compiles("public class A { public int M() => 1; }");
    }

    [Fact]
    public void RejectsSyntaxError()
    {
        var errors = RoslynAssert.Errors("public class A { public int M() => 1 }");

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void RejectsBindingError()
    {
        var errors = RoslynAssert.Errors("public class A { public int M() => NoSuchThing; }");

        Assert.Contains(errors, e => e.Id == "CS0103");
    }

    [Fact]
    public void RejectsUnescapedStringLiteral()
    {
        // The §7 defect, stated as the compiler sees it.
        var errors = RoslynAssert.Errors("public class A { string S = \"he said \"hi\"\"; }");

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ReferencesResolveAgainstTheBaseClassLibrary()
    {
        RoslynAssert.MemberCompiles("public List<int> Items = new List<int>();");
    }

    [Fact]
    public void MemberHarnessRejectsBadMembers()
    {
        var errors = RoslynAssert.Errors("public class A { public abstract void M() { } }");

        Assert.Contains(errors, e => e.Id == "CS0500" || e.Id == "CS0513");
    }
}
