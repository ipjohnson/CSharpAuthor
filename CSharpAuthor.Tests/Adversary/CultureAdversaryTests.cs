using System;
using Xunit;
using static CSharpAuthor.SyntaxHelpers;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// Every numeric emission, run on de-DE.
/// </summary>
/// <remarks>
/// <para>
/// §7 records that numbers are culture-dependent. What it does not record is how many separate
/// places do it, and that one of them changes the meaning of the output instead of breaking it: on
/// de-DE the decimal separator is a comma, and a comma is C#'s argument separator. <c>1.5</c>
/// becomes <c>1,5</c>, which in an argument list is two arguments. Where an overload with that
/// arity exists, it compiles, and the generated code calls a different method with different values.
/// That is the whole defect class in one line.
/// </para>
/// <para>
/// The culture is installed per test rather than for the assembly, so a failure cannot leak into
/// the tests that run after it.
/// </para>
/// </remarks>
public class CultureAdversaryTests
{
    private const string German = "de-DE";

    [Fact(Skip = "ADVERSARY GAP (§7 'Culture-dependent numbers'): CodeOutputComponent.Get(1.5d) uses the ambient culture - 1,5 on de-DE")]
    public void DoubleValue()
    {
        var output = Emit.InCulture(German, () => Emit.Component(CodeOutputComponent.Get(1.5d)));

        Assert.Equal("1.5", output);
    }

    [Fact(Skip = "ADVERSARY GAP: the float path has the same ambient-culture ToString - 1,5 on de-DE")]
    public void FloatValue()
    {
        var output = Emit.InCulture(German, () => Emit.Component(CodeOutputComponent.Get(1.5f)));

        Assert.Equal("1.5", output);
    }

    [Fact(Skip = "ADVERSARY GAP: the decimal path has the same ambient-culture ToString - 1,5 on de-DE")]
    public void DecimalValue()
    {
        var output = Emit.InCulture(German, () => Emit.Component(CodeOutputComponent.Get(1.5m)));

        Assert.Equal("1.5", output);
    }

    /// <summary>
    /// <c>EnumValueDefinition</c> calls <c>Value.ToString()</c> itself rather than going through the
    /// component, so it is a second site with the same defect.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: EnumValueDefinition calls Value.ToString() with the ambient culture, emitting 'A = 1,5,' - which the parser reads as a member A = 1 followed by a member named 5")]
    public void EnumMemberValue()
    {
        var output = Emit.InCulture(German, () =>
        {
            var enumDefinition = new EnumDefinition("E");

            enumDefinition.AddValue("A", 1.5d);

            return Emit.Component(enumDefinition);
        });

        RoslynAssert.Compiles(output);
    }

    /// <summary>
    /// The case that compiles. A one-argument attribute becomes a two-argument attribute, and
    /// <see cref="AttributeUsageAttribute"/> is only one of many that has both arities.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: an attribute argument written on de-DE splits into two arguments at the decimal comma - the output compiles when an overload of that arity exists and calls something else entirely")]
    public void AttributeArgumentDoesNotSplitIntoTwo()
    {
        var output = Emit.InCulture(German, () =>
        {
            var classDefinition = new ClassDefinition("Host");

            classDefinition.AddAttribute(TypeDefinition.Get("Probe", "MeasureAttribute"), 1.5d);

            return Emit.Component(classDefinition);
        });

        Assert.Contains("Measure(1.5)", output);
    }

    [Fact]
    public void AddCodeRawArgument()
    {
        var output = Emit.InCulture(German, () =>
        {
            var method = new MethodDefinition("M");

            method.AddCode("var x = [arg1];", 1.5d);

            return Emit.Component(method);
        });

        RoslynAssert.MemberCompiles(output);
    }

    [Fact]
    public void FieldInitializer()
    {
        var output = Emit.InCulture(German, () =>
        {
            var classDefinition = new ClassDefinition("Host");

            classDefinition.AddField(typeof(double), "f").InitializeValue =
                CodeOutputComponent.Get(1.5d);

            return Emit.Component(classDefinition);
        });

        RoslynAssert.Compiles(output);
    }

    [Fact(Skip = "ADVERSARY GAP: a constructor argument written from a double uses the ambient culture - new Point(1,5) is a two-argument call")]
    public void ConstructorArgument()
    {
        var output = Emit.InCulture(German, () =>
            Emit.Component(New(TypeDefinition.Get("Probe", "Point"), 1.5d)));

        Assert.Equal("new Point(1.5)", output);
    }

    /// <summary>
    /// Integers carry no separator, so they survive any culture. Unskipped: an invariant-culture fix
    /// must not start emitting group separators.
    /// </summary>
    [Fact]
    public void IntegersAreUnaffected()
    {
        var output = Emit.InCulture(German, () =>
            Emit.Component(CodeOutputComponent.Get(1234567)));

        Assert.Equal("1234567", output);
    }

    /// <summary>
    /// An array length is written from an <see cref="int"/>, so it is safe. Unskipped for the same
    /// reason.
    /// </summary>
    [Fact]
    public void ArrayLengthIsUnaffected()
    {
        var output = Emit.InCulture(German, () => Emit.Component(NewArray(typeof(int), 5000)));

        Assert.Equal("new int[5000]", output);
    }

    /// <summary>
    /// Indent widths, line numbers and every other internal count are ints too. This asserts a whole
    /// generated file is byte-identical between cultures, which is the property the fix should
    /// actually establish - stronger than checking the sites one at a time, and it will catch a
    /// site nobody thought of.
    /// </summary>
    [Fact]
    public void AWholeFileIsIdenticalAcrossCultures()
    {
        static string Build()
        {
            var file = new CSharpFileDefinition("Probe");

            var classDefinition = file.AddClass("Host");

            classDefinition.AddField(typeof(double), "rate").InitializeValue =
                CodeOutputComponent.Get(0.125d);

            classDefinition.AddField(typeof(decimal), "price").InitializeValue =
                CodeOutputComponent.Get(19.99m);

            return Emit.File(file);
        }

        var invariant = Emit.InCulture("en-US", Build);
        var german = Emit.InCulture(German, Build);

        Assert.Equal(invariant, german);
    }
}
