using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CSharpAuthor.Tests.PropertyDefinitionTests
{
    public class SimplePropertyDefinitionTests
    {
        [Fact]
        public void PropertyDefinition()
        {
            var propertyDefinition = new PropertyDefinition( TypeDefinition.Get(typeof(int)), "Test");

            var context = new OutputContext();

            propertyDefinition.WriteOutput(context);

            AssertEqual.WithoutNewLine(PropertyDefinitionOutput, context.Output());
        }

        private const string PropertyDefinitionOutput =
 @"public int Test { get; set; }
";

        [Fact]
        public void LambdaGetDefinition()
        {
            var propertyDefinition = new PropertyDefinition(TypeDefinition.Get(typeof(int)), "Test");

            propertyDefinition.Get.LambdaSyntax = true;
            propertyDefinition.Set = null;

            propertyDefinition.Get.AddCode("10;");
            var context = new OutputContext();

            propertyDefinition.WriteOutput(context);

            AssertEqual.WithoutNewLine(LambdaGetDefinitionOutput, context.Output());
        }

        private const string LambdaGetDefinitionOutput =
@"public int Test => 10;
";

        [Fact]
        public void LambdaGetWithSetDefinition()
        {
            var propertyDefinition = new PropertyDefinition(TypeDefinition.Get(typeof(int)), "Test");

            propertyDefinition.Get.LambdaSyntax = true;
            propertyDefinition.Get.AddCode("_value;");

            propertyDefinition.Set.LambdaSyntax = true;
            propertyDefinition.Set.AddCode("_value = value;");

            var context = new OutputContext();

            propertyDefinition.WriteOutput(context);

            AssertEqual.WithoutNewLine(LambdaGetWithSetDefinitionOutput, context.Output());
        }

        private const string LambdaGetWithSetDefinitionOutput =
            @"public int Test
{
    get => _value;
    set => _value = value;
}
";

        [Fact]
        public void GetSetDefinition()
        {
            var propertyDefinition = new PropertyDefinition(TypeDefinition.Get(typeof(int)), "Test");
            
            propertyDefinition.Get.AddCode("return _value;");
            
            propertyDefinition.Set.AddCode("_value = value;");
            propertyDefinition.Set.Modifiers |= ComponentModifier.Private;

            var context = new OutputContext();

            propertyDefinition.WriteOutput(context);

            AssertEqual.WithoutNewLine(GetSetDefinitionOutput, context.Output());
        }

        private const string GetSetDefinitionOutput =
            @"public int Test
{
    get
    {
        return _value;
    }
    private set
    {
        _value = value;
    }
}
";

        [Fact]
        public void PrivateStaticGetSetDefinition()
        {
            var propertyDefinition = new PropertyDefinition(TypeDefinition.Get(typeof(int)), "Test");

            propertyDefinition.Modifiers |= ComponentModifier.Static | ComponentModifier.Private;

            propertyDefinition.Get.AddCode("return _value;");

            propertyDefinition.Set.AddCode("_value = value;");

            var context = new OutputContext();

            propertyDefinition.WriteOutput(context);

            AssertEqual.WithoutNewLine(PrivateStaticGetSetDefinitionOutput, context.Output());
        }

        private const string PrivateStaticGetSetDefinitionOutput =
            @"private static int Test
{
    get
    {
        return _value;
    }
    set
    {
        _value = value;
    }
}
";
        
        [Fact]
        public void ProtectedVirtualGetSetDefinition()
        {
            var propertyDefinition = new PropertyDefinition(TypeDefinition.Get(typeof(int)), "Test");

            propertyDefinition.Modifiers |= ComponentModifier.Virtual | ComponentModifier.Protected;

            propertyDefinition.Get.AddCode("return _value;");

            propertyDefinition.Set.AddCode("_value = value;");

            var context = new OutputContext();

            propertyDefinition.WriteOutput(context);

            AssertEqual.WithoutNewLine(ProtectedVirtualGetSetDefinitionOutput, context.Output());
        }

        private const string ProtectedVirtualGetSetDefinitionOutput =
@"protected virtual int Test
{
    get
    {
        return _value;
    }
    set
    {
        _value = value;
    }
}
";

        [Fact]
        public void PublicOverrideGetSetDefinition()
        {
            var propertyDefinition = new PropertyDefinition(TypeDefinition.Get(typeof(int)), "Test");

            propertyDefinition.Modifiers |= ComponentModifier.Override | ComponentModifier.Public;

            propertyDefinition.Get.AddCode("return _value;");

            propertyDefinition.Set.AddCode("_value = value;");

            var context = new OutputContext();

            propertyDefinition.WriteOutput(context);

            AssertEqual.WithoutNewLine(PublicOverrideGetSetDefinitionOutput, context.Output());
        }

        private const string PublicOverrideGetSetDefinitionOutput =
@"public override int Test
{
    get
    {
        return _value;
    }
    set
    {
        _value = value;
    }
}
";

        [Fact]
        public void IndexedGetSetDefinition()
        {
            var propertyDefinition = new PropertyDefinition(TypeDefinition.Get(typeof(int)), "Test");

            propertyDefinition.IndexType = TypeDefinition.Get(typeof(string));

            propertyDefinition.Get.AddCode("return _value;");

            propertyDefinition.Set.AddCode("_value = value;");

            var context = new OutputContext();

            propertyDefinition.WriteOutput(context);

            AssertEqual.WithoutNewLine(IndexedGetSetDefinitionOutput, context.Output());
        }

        private const string IndexedGetSetDefinitionOutput =
@"public int Test[string index]
{
    get
    {
        return _value;
    }
    set
    {
        _value = value;
    }
}
";
    }
}
