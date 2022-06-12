﻿using System;
using Xunit;

namespace CSharpAuthor.Tests.MethodTests
{
    public class MethodDefinitionGenerateTests
    {
        #region HelloWorld
        
        [Fact]
        public void GenerateHelloWorldMethod()
        {
            var methodDefinition = new MethodDefinition("HelloWorld")
                .SetReturnType(typeof(string));

            methodDefinition.AddStatement("return \"Hello World\";");
            var block = methodDefinition.ForEach("var test in blah");
            
            var context = new OutputContext();

            methodDefinition.WriteOutput(context);

            var outputString = context.Output();

            AssertEqual.WithoutNewLine(helloWorldExpectedOutput, outputString);
        }

        private static string helloWorldExpectedOutput = 
@"public string HelloWorld()
{
    return ""Hello World"";
}
";
        #endregion

        #region Void return

        [Fact]
        public void GenerateVoidMethod()
        {
            var methodDefinition = new MethodDefinition("HelloWorld");
            
            var context = new OutputContext();

            methodDefinition.WriteOutput(context);

            var outputString = context.Output();

            AssertEqual.WithoutNewLine(voidMethodExpectedOutput, outputString);
        }

        private static string voidMethodExpectedOutput = 
@"public void HelloWorld()
{
}
";
        #endregion

        #region AddStatement ITypeDefinition

        [Fact]
        public void AddTypeDefinitionTest()
        {
            var methodDefinition = new MethodDefinition("HelloWorld")
                .SetReturnType(typeof(string));

            methodDefinition.AddStatement("return {arg1} + typeof({arg2}) + {arg3};", "Hello World", typeof(string), 15);

            var context = new OutputContext();

            methodDefinition.WriteOutput(context);

            var outputString = context.Output();

            AssertEqual.WithoutNewLine(addTypeDefinitionOutput, outputString);
        }

        private static string addTypeDefinitionOutput =
@"public string HelloWorld()
{
    return ""Hello World"" + typeof(string) + 15;
}
";
        #endregion
    }
}
