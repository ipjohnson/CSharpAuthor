using System;
using System.Globalization;
using System.Threading;
using Xunit;

namespace CSharpAuthor.Tests.LiteralTests;

/// <summary>
/// Every numeric emission, asserted under a culture that formats numbers differently from the
/// invariant one.
/// </summary>
/// <remarks>
/// This is the defect that only ever appears on someone else's machine. <c>value.ToString()</c>
/// honours <see cref="CultureInfo.CurrentCulture"/>, so a source generator on a de-DE machine wrote
/// <c>1,5</c> where the file needed <c>1.5</c>, and the CI box that formats in en-US never saw it.
/// sv-SE is included because its negative sign is U+2212, not the hyphen-minus C# expects, so even
/// an <c>int</c> is culture dependent.
/// </remarks>
public class CultureTests
{
    private static void InCulture(string cultureName, Action action)
    {
        var original = Thread.CurrentThread.CurrentCulture;

        try
        {
            var culture = new CultureInfo(cultureName);

            Thread.CurrentThread.CurrentCulture = culture;
            CultureInfo.CurrentCulture = culture;

            action();
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void GermanCultureDoesNotChangeTheDecimalSeparator()
    {
        InCulture("de-DE", () =>
        {
            // Guards the premise: if this ever stops holding, the test below proves nothing.
            Assert.Equal("1,5", 1.5.ToString());

            Assert.Equal("1.5f", LiteralFormatter.Format(1.5f));
            Assert.Equal("1.5d", LiteralFormatter.Format(1.5d));
            Assert.Equal("1.5m", LiteralFormatter.Format(1.5m));
        });
    }

    [Fact]
    public void GermanCultureDoesNotChangeIntegerTypes()
    {
        InCulture("de-DE", () =>
        {
            Assert.Equal("1234567", LiteralFormatter.Format(1234567));
            Assert.Equal("1234567L", LiteralFormatter.Format(1234567L));
            Assert.Equal("1234567U", LiteralFormatter.Format(1234567U));
            Assert.Equal("1234567UL", LiteralFormatter.Format(1234567UL));
            Assert.Equal("12", LiteralFormatter.Format((short)12));
            Assert.Equal("12", LiteralFormatter.Format((byte)12));
            Assert.Equal("12", LiteralFormatter.Format((sbyte)12));
            Assert.Equal("12", LiteralFormatter.Format((ushort)12));
        });
    }

    [Fact]
    public void SwedishCultureDoesNotChangeTheNegativeSign()
    {
        InCulture("sv-SE", () =>
        {
            Assert.Equal("-5", LiteralFormatter.Format(-5));
            Assert.Equal("-5L", LiteralFormatter.Format(-5L));
            Assert.Equal("-1.5f", LiteralFormatter.Format(-1.5f));
            Assert.Equal("-1.5d", LiteralFormatter.Format(-1.5d));
            Assert.Equal("-1.5m", LiteralFormatter.Format(-1.5m));
        });
    }

    [Fact]
    public void FieldInitializerIsInvariantUnderGermanCulture()
    {
        InCulture("de-DE", () =>
        {
            var field = new FieldDefinition(TypeDefinition.Get(typeof(double)), "_rate")
            {
                InitializeValue = CodeOutputComponent.Get(1.5d)
            };

            var outputContext = new OutputContext();

            field.WriteOutput(outputContext);

            AssertEqual.ContainsWithoutNewLine("= 1.5d;", outputContext.Output());
        });
    }

    [Fact]
    public void EnumValueIsInvariantUnderGermanCulture()
    {
        InCulture("de-DE", () =>
        {
            var enumDefinition = new EnumDefinition("Sizes");

            enumDefinition.AddValue("Large", -1024);

            var outputContext = new OutputContext();

            enumDefinition.WriteOutput(outputContext);

            AssertEqual.ContainsWithoutNewLine("Large = -1024,", outputContext.Output());
        });
    }

    [Fact]
    public void ArrayElementsAreInvariantUnderGermanCulture()
    {
        InCulture("de-DE", () =>
        {
            var array = SyntaxHelpers.NewArray(typeof(double), 1.5d, 2.25d);

            var outputContext = new OutputContext();

            array.WriteOutput(outputContext);

            AssertEqual.WithoutNewLine("new double[] { 1.5d, 2.25d }", outputContext.Output());
        });
    }

    [Fact]
    public void ArrayLengthIsInvariantUnderSwedishCulture()
    {
        InCulture("sv-SE", () =>
        {
            var array = SyntaxHelpers.NewArray(typeof(int), 10);

            var outputContext = new OutputContext();

            array.WriteOutput(outputContext);

            AssertEqual.WithoutNewLine("new int[10]", outputContext.Output());
        });
    }

    [Fact]
    public void AddCodeSubstitutionIsInvariantUnderGermanCulture()
    {
        InCulture("de-DE", () =>
        {
            var method = new MethodDefinition("Test");

            method.AddCode("var x = [arg1];", 1.5d);

            var outputContext = new OutputContext();

            method.WriteOutput(outputContext);

            AssertEqual.ContainsWithoutNewLine("var x = 1.5d;", outputContext.Output());
        });
    }
}
