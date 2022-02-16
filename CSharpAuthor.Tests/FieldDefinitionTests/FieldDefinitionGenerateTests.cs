using Xunit;

namespace CSharpAuthor.Tests.FieldDefinitionTests
{
    public class FieldDefinitionGenerateTests
    {
        [Fact]
        public void WriteFieldTest()
        {
            var fieldDefinition = new FieldDefinition(typeof(string), "field");

            var context = new OutputContext();

            context.IncrementIndent();

            fieldDefinition.WriteOutput(context);

            var outputString = context.Output();

            AssertEqual.WithoutNewLine(expectedWriteFieldString, outputString);
        }

        private static readonly string expectedWriteFieldString = 
@"    private string field;
";
    }
}
