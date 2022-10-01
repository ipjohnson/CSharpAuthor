using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CSharpAuthor.Tests.MethodTests;

public class MethodBlockTests
{
    [Fact]
    public void TryCatchTest()
    {
        var method = new MethodDefinition("Test");

        var tryBlock = method.Try();

        tryBlock.Assign("10").To("var test");
        tryBlock.Throw(typeof(Exception), "\"message\"");

        tryBlock.Catch(typeof(Exception), "e").AddCode("e.ToString();");

        tryBlock.Finally().AddCode("// got here");

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        AssertEqual.WithoutNewLine(TryCatchExpected, outputContext.Output());
    }

    private const string TryCatchExpected = 
        @"public void Test()
{
    try
    {
        var test = 10;
        throw new Exception(""message"");
    }
    catch (Exception e)
    {
        e.ToString();
    }
    finally
    {
        // got here
    }
}
";

    [Fact]
    public void ForEachTest()
    {
        var method = new MethodDefinition("Test");

        var collection = method.AddParameter(typeof(IEnumerable<object>), "collection");

        var forEachBlock = method.ForEach("someValue", collection);

        var someValue = forEachBlock.Instance;
            
        forEachBlock.Assign(someValue.Invoke("ToString")).ToVar("someField");

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        AssertEqual.WithoutNewLine(ForEachExpected, outputContext.Output());
    }

    private const string ForEachExpected = 
        @"public void Test(IEnumerable<object> collection)
{
    foreach(var someValue in collection)
    {
        var someField = someValue.ToString();
    }
}
";

    [Fact]
    public void IfElseIfBlockTest()
    {
        var method = new MethodDefinition("Test");

        var ifBlock = method.If("x > 100");

        ifBlock.AddCode("Console.WriteLine(\"Over 100\");");

        ifBlock.ElseIf("x > 50").AddCode("Console.WriteLine(\"Over 50\");");

        ifBlock.Else().AddCode("Console.WriteLine(\"50 and under\");");

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        AssertEqual.WithoutNewLine(IfElseExpected, outputContext.Output());
    }

    private const string IfElseExpected =
        @"public void Test()
{
    if (x > 100)
    {
        Console.WriteLine(""Over 100"");
    }
    else if (x > 50)
    {
        Console.WriteLine(""Over 50"");
    }
    else
    {
        Console.WriteLine(""50 and under"");
    }
}
";

    [Fact]
    public void AssignToTest()
    {

    }
}