using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CSharpAuthor.Tests.MethodTests
{
    public class MethodBlockTests
    {
        [Fact]
        public void TryCatchTest()
        {
            var method = new MethodDefinition("Test");

            var tryBlock = method.Try();

            tryBlock.Assign("10").To("var test");
            tryBlock.Throw(typeof(Exception), "\"message\"");

            tryBlock.Catch(typeof(Exception), "e").AddStatement("e.ToString();");

            tryBlock.Finally().AddStatement("// got here");

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

            var forEachBlock = method.ForEach("var someValue in Collection");

            forEachBlock.Assign("someValue.ToString()").To("var someField");

            var outputContext = new OutputContext();

            method.WriteOutput(outputContext);

            AssertEqual.WithoutNewLine(ForEachExpected, outputContext.Output());
        }

        private const string ForEachExpected = 
@"public void Test()
{
    foreach(var someValue in Collection)
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

            ifBlock.AddStatement("Console.WriteLine(\"Over 100\");");

            ifBlock.ElseIf("x > 50").AddStatement("Console.WriteLine(\"Over 50\");");

            ifBlock.Else().AddStatement("Console.WriteLine(\"50 and under\");");

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
    }
}
