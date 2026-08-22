using System;
using Xunit;
using static CSharpAuthor.SyntaxHelpers;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// Every numeric emission, run on de-DE.
/// </summary>
/// <remarks>
/// <para>
/// §7 recorded numbers as culture-dependent, and the reason it mattered is that one of the sites
/// changed the meaning of the output instead of breaking it: on de-DE the decimal separator is a
/// comma, and a comma is C#'s argument separator. <c>1.5</c> became <c>1,5</c>, which in an
/// argument list is two arguments. Where an overload of that arity existed, it compiled, and the
/// generated code called a different method with different values.
/// </para>
/// <para>
/// That defect is fixed: <see cref="LiteralFormatter"/> formats every numeric invariantly and
/// suffixes it with the character that fixes its type. These tests are the regression barrier, so
/// they assert the exact literal text - <c>1.5f</c> and not <c>1.5</c>, because a bare <c>1.5</c>
/// is a <see cref="double"/> and would silently change the type of a <see cref="float"/> or
/// <see cref="decimal"/> emission.
/// </para>
/// <para>
/// The culture is installed per test rather than for the assembly, so a failure cannot leak into
/// the tests that run after it.
/// </para>
/// </remarks>
public class CultureAdversaryTests
{
    private const string German = "de-DE";

    [Fact]
    public void DoubleValue()
    {
        var output = Emit.InCulture(German, () => Emit.Component(CodeOutputComponent.Get(1.5d)));

        Assert.Equal("1.5d", output);
    }

    /// <summary>
    /// The <c>f</c> is not decoration: a bare <c>1.5</c> is a <see cref="double"/>, so dropping the
    /// suffix would change the type of every emitted <see cref="float"/>.
    /// </summary>
    [Fact]
    public void FloatValue()
    {
        var output = Emit.InCulture(German, () => Emit.Component(CodeOutputComponent.Get(1.5f)));

        Assert.Equal("1.5f", output);
    }

    /// <summary>Same again: without <c>m</c> this is a <see cref="double"/>.</summary>
    [Fact]
    public void DecimalValue()
    {
        var output = Emit.InCulture(German, () => Emit.Component(CodeOutputComponent.Get(1.5m)));

        Assert.Equal("1.5m", output);
    }

    /// <summary>
    /// <c>EnumValueDefinition</c> writes its value itself rather than going through the component,
    /// so it is a second site that has to be invariant.
    /// </summary>
    /// <remarks>
    /// The value is a large <see cref="int"/> rather than a fractional one because an enum member
    /// is integral: a decimal separator cannot arise here, but a *group* separator can, and that is
    /// what an ambient-culture <c>ToString</c> would add.
    /// </remarks>
    [Fact]
    public void EnumMemberValue()
    {
        var output = Emit.InCulture(German, () =>
        {
            var enumDefinition = new EnumDefinition("E");

            enumDefinition.AddValue("A", 1234567);

            return Emit.Component(enumDefinition);
        });

        Assert.Contains("A = 1234567", output);
        RoslynAssert.Compiles(output);
    }

    /// <summary>
    /// The case that compiles. A one-argument attribute becomes a two-argument attribute, and
    /// <see cref="AttributeUsageAttribute"/> is only one of many that has both arities.
    /// </summary>
    [Fact]
    public void AttributeArgumentDoesNotSplitIntoTwo()
    {
        var output = Emit.InCulture(German, () =>
        {
            var classDefinition = new ClassDefinition("Host");

            classDefinition.AddAttribute(TypeDefinition.Get("Probe", "MeasureAttribute"), 1.5d);

            return Emit.Component(classDefinition);
        });

        Assert.Contains("Measure(1.5d)", output);
        Assert.DoesNotContain("1,5", output);
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

    [Fact]
    public void ConstructorArgument()
    {
        var output = Emit.InCulture(German, () =>
            Emit.Component(New(TypeDefinition.Get("Probe", "Point"), 1.5d)));

        Assert.Equal("new Point(1.5d)", output);
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
