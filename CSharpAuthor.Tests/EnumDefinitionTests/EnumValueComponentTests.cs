using CSharpAuthor.Expressions;
using CSharpAuthor.Tests.Adversary;
using Xunit;

namespace CSharpAuthor.Tests.EnumDefinitionTests;

/// <summary>
/// <see cref="EnumValueDefinition.Value"/> takes a component, not only a literal.
/// </summary>
/// <remarks>
/// It used to format the object it was handed directly, so anything that was not a literal fell
/// through to its own <c>ToString()</c> and the member was written as a class name -
/// <c>A = CSharpAuthor.Expressions.Ex</c>. The call succeeded and the output looked plausible, so
/// the error landed in the consumer's build. Every other value API in the library funnels through
/// <see cref="CodeOutputComponent.Get(object,bool)"/>, and this one now does too.
/// </remarks>
public class EnumValueComponentTests
{
    [Fact]
    public void FlagValuesBuiltFromAShiftAreWritten()
    {
        var enumDefinition = new EnumDefinition("Access");

        enumDefinition.AddValue("None", 0);
        enumDefinition.AddValue("Read", Ex.ShiftLeft(1, 0));
        enumDefinition.AddValue("Write", Ex.ShiftLeft(1, 1));
        enumDefinition.AddValue("Execute", Ex.ShiftLeft(1, 2));

        var output = Emit.Component(enumDefinition);

        Assert.Contains("None = 0,", output);
        Assert.Contains("Read = 1 << 0,", output);
        Assert.Contains("Write = 1 << 1,", output);
        Assert.Contains("Execute = 1 << 2,", output);

        // The failure this replaces.
        Assert.DoesNotContain("CSharpAuthor.Expressions.Ex", output);
    }

    [Fact]
    public void ACombinationOfEarlierMembersIsWritten()
    {
        var enumDefinition = new EnumDefinition("Access");

        enumDefinition.AddValue("Read", 1);
        enumDefinition.AddValue("Write", 2);
        enumDefinition.AddValue("ReadWrite", Ex.BitOr("Read", "Write"));

        Assert.Contains("ReadWrite = Read | Write,", Emit.Component(enumDefinition));
    }

    [Fact]
    public void APlainLiteralStillGoesThroughLiteralFormatter()
    {
        var enumDefinition = new EnumDefinition("Sized");

        enumDefinition.AddValue("Big", 1L);

        // Unchanged behaviour: the numeric suffix is LiteralFormatter's, and it stays.
        Assert.Contains("Big = 1L,", Emit.Component(enumDefinition));
    }
}
