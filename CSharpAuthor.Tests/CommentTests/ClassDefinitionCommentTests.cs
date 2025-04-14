using Xunit;

namespace CSharpAuthor.Tests.CommentTests;

public class ClassDefinitionCommentTests
{
    [Fact]
    public void ClassComment()
    {
        var classDefinition = new ClassDefinition("MyClass")
        {
            Comment = "This is a class"
        };
        
        var prop = classDefinition.AddProperty(typeof(int), "IntProp");
        
        prop.Comment = "This is an int property";
        
        var method = classDefinition.AddMethod("MyMethod");

        method.Comment = "This is a method";
        var parameter = method.AddParameter(typeof(int), "param");
        parameter.Comment = "This is a parameter";
        method.SetReturnType(typeof(int));
        method.ReturnComment = "This is the return value";

        var output = new OutputContext();
        classDefinition.WriteOutput(output);
        
        AssertEqual.WithoutNewLine(expected, output.Output());
    }

    private const string expected =
        @"/// <summary>
/// This is a class
/// </summary>
public class MyClass
{

    /// <summary>
    /// This is an int property
    /// </summary>
    public int IntProp { get; set; }

    /// <summary>
    /// This is a method
    /// </summary>
    /// <param name=""param"">This is a parameter</param>
    /// <returns>This is the return value</returns>
    public int MyMethod(int param)
    {
    }
}
";
}