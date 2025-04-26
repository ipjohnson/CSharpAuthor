using Xunit;

namespace CSharpAuthor.Tests.NamespaceTests;

public class NestedNamespaceTests
{
    [Fact]
    public void NestedNamespace()
    {
        var namespaceDefinition = new NamespaceDefinition("Base");

        namespaceDefinition.AddNamespace("SubA");
        namespaceDefinition.AddNamespace("SubB");
        var subC = namespaceDefinition.AddNamespace("SubC");

        var interfaceC = subC.AddInterface("IC");

        var context = new OutputContext();
        
        namespaceDefinition.WriteOutput(context);
        
        AssertEqual.WithoutNewLine(nestedOutput, context.Output());
    }
    
    private const string nestedOutput = @"namespace Base
{
    namespace SubA
    {
    }

    namespace SubB
    {
    }

    namespace SubC
    {
        public interface IC
        {
        }
    }
}
";
}