using Xunit;

namespace CSharpAuthor.Tests.ClassDefinitionTests;

/// <summary>
/// The blank line between members used to be written before each member rather than between them,
/// which put one directly under the opening brace of every generated type.
/// </summary>
public class ClassBodySpacingTests
{
    [Fact]
    public void FirstMemberFollowsTheOpeningBrace()
    {
        var classDefinition = new ClassDefinition("Holder");

        classDefinition.AddMethod("First");
        classDefinition.AddMethod("Second");

        AssertEqual.WithoutNewLine(FirstMemberExpected, Write(classDefinition));
    }

    private const string FirstMemberExpected =
        @"public class Holder
{
    public void First()
    {
    }

    public void Second()
    {
    }
}
";

    [Fact]
    public void FieldsPackTogetherAndTheNextMemberIsSeparated()
    {
        var classDefinition = new ClassDefinition("Holder");

        classDefinition.AddField(TypeDefinition.Get(typeof(string)), "_first");
        classDefinition.AddField(TypeDefinition.Get(typeof(string)), "_second");
        classDefinition.AddMethod("Work");

        AssertEqual.WithoutNewLine(FieldsExpected, Write(classDefinition));
    }

    private const string FieldsExpected =
        @"public class Holder
{
    private string _first;
    private string _second;

    public void Work()
    {
    }
}
";

    [Fact]
    public void EmptyClassHasNoBlankBody()
    {
        AssertEqual.WithoutNewLine("public class Holder\n{\n}\n", Write(new ClassDefinition("Holder")));
    }

    private static string Write(IOutputComponent component)
    {
        var context = new OutputContext();

        component.WriteOutput(context);

        return context.Output();
    }
}
