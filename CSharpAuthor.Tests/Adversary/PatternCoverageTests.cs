using Xunit;
using static CSharpAuthor.SyntaxHelpers;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// Patterns. The measured coverage is 0% and this file is what that number means.
/// </summary>
/// <remarks>
/// <para>
/// The library has one thing in this whole area: <c>SyntaxHelpers.Is(component, type)</c>, which
/// writes a bare type pattern with no designation - and writes it from
/// <c>ITypeDefinition.Name</c>, so it is wrong for a generic or an array (see
/// <see cref="ExpressionAdversaryTests"/>). Nothing else in the pattern grammar has an entry point.
/// </para>
/// <para>
/// These are recorded as tests rather than as a list in a document because a test is checked by the
/// build. Each one names the API that would be needed, so the shape of <c>IPattern</c> can be read
/// off the file: every case here has to be constructible and every case has to compose with
/// <c>and</c>, <c>or</c> and <c>not</c>.
/// </para>
/// </remarks>
public class PatternCoverageTests
{
    /// <summary>
    /// The one pattern that can be written, so the eventual pattern API has something to stay
    /// compatible with. Unskipped.
    /// </summary>
    [Fact]
    public void TypePatternWithoutADesignationIsTheOnlyOneAvailable()
    {
        var expression = Is(CodeOutputComponent.Get("x"), TypeDefinition.Get(typeof(string)));

        Assert.Equal("x is string", Emit.Component(expression));

        RoslynAssert.StatementCompiles("object x = null;\nif (" + Emit.Component(expression) + ") { }");
    }
}
