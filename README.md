# CSharpAuthor

This is a simple library for programmatically generating CSharp code. The main use case is for C# source generators. 

```
 var file = new CSharpFileDefinition("TestNamespace");

  var classDefinition = file.AddClass("TestClass");

 var method = classDefinition.AddMethod("SomeMethod");

 classDefinition.AddUsingNamespace("SomeNamespace");
            
 var outputContext = new OutputContext();

 file.WriteOutput(outputContext);

 var outputString = outputContext.Output();
            
Generates

 using SomeNamespace;

namespace TestNamespace
{
    public class TestClass
    {

        public void SomeMethod()
        {
        }
    }
}

```