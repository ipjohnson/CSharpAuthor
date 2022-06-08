using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CSharpAuthor.Tests.InterfaceTests
{
    public class SimpleInterfaceTests
    {
        [Fact]
        public void InterfaceWithMethod()
        {
            var interfaceDefinition = new InterfaceDefinition("Test");

            var addMethod = interfaceDefinition.AddMethod("Add");

            addMethod.AddParameter(typeof(int), "x");
            addMethod.AddParameter(typeof(int), "y");
            addMethod.SetReturnType(typeof(int));

            var subtractMethod = interfaceDefinition.AddMethod("Subtract");

            subtractMethod.AddParameter(typeof(int), "x");
            subtractMethod.AddParameter(typeof(int), "y");
            subtractMethod.SetReturnType(typeof(int));

            var context = new OutputContext();

            interfaceDefinition.WriteOutput(context);

            AssertEqual.WithoutNewLine(InterfaceWithMethodExpected, context.Output());
        }

        private const string InterfaceWithMethodExpected =
@"public interface Test
{
    int Add(int x, int y);
    int Subtract(int x, int y);
}
";
        
        [Fact]
        public void InterfaceWithProperty()
        {
            var interfaceDefinition = new InterfaceDefinition("Test");

            interfaceDefinition.AddProperty(typeof(string), "StringA");

            interfaceDefinition.AddProperty(typeof(string), "StringB");

            var context = new OutputContext();

            interfaceDefinition.WriteOutput(context);

            AssertEqual.WithoutNewLine(InterfaceWithPropertyExpected, context.Output());
        }

        private const string InterfaceWithPropertyExpected =
            @"public interface Test
{
    string StringA { get; set; }
    string StringB { get; set; }
}
";
    }
}
