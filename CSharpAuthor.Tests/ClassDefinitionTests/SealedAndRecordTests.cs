using Xunit;

namespace CSharpAuthor.Tests.ClassDefinitionTests;

public class SealedAndRecordTests
{
    [Fact]
    public void SealedClass()
    {
        var classDefinition = new ClassDefinition("TestClass");
        classDefinition.Modifiers |= ComponentModifier.Sealed;

        var context = new OutputContext();
        classDefinition.WriteOutput(context);

        AssertEqual.WithoutNewLine(SealedClassOutput, context.Output());
    }

    private const string SealedClassOutput =
        @"public sealed class TestClass
{
}
";

    [Fact]
    public void RecordType()
    {
        var classDefinition = new ClassDefinition("TestRecord");
        classDefinition.TypeKeyword = ClassKeyword.Record;

        var context = new OutputContext();
        classDefinition.WriteOutput(context);

        AssertEqual.WithoutNewLine(RecordOutput, context.Output());
    }

    private const string RecordOutput =
        @"public record TestRecord
{
}
";

    [Fact]
    public void SealedRecord()
    {
        var classDefinition = new ClassDefinition("TestRecord");
        classDefinition.TypeKeyword = ClassKeyword.Record;
        classDefinition.Modifiers |= ComponentModifier.Sealed;

        classDefinition.AddProperty(TypeDefinition.Get(typeof(string)), "Name");

        var context = new OutputContext();
        classDefinition.WriteOutput(context);

        AssertEqual.WithoutNewLine(SealedRecordOutput, context.Output());
    }

    private const string SealedRecordOutput =
        @"public sealed record TestRecord
{
    public string Name { get; set; }
}
";

    [Fact]
    public void RecordStruct()
    {
        var classDefinition = new ClassDefinition("Point");
        classDefinition.TypeKeyword = ClassKeyword.RecordStruct;

        var context = new OutputContext();
        classDefinition.WriteOutput(context);

        AssertEqual.WithoutNewLine(RecordStructOutput, context.Output());
    }

    private const string RecordStructOutput =
        @"public record struct Point
{
}
";
}
