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
            var propertyDefinition = new PropertyDefinition("Test", TypeDefinition.Get(typeof(int)));

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
            var propertyDefinition = new PropertyDefinition("Test", TypeDefinition.Get(typeof(int)));

            propertyDefinition.Get.LambdaSyntax = true;
            propertyDefinition.Set = null;

            propertyDefinition.Get.AddStatement("10;");
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
            var propertyDefinition = new PropertyDefinition("Test", TypeDefinition.Get(typeof(int)));

            propertyDefinition.Get.LambdaSyntax = true;
            propertyDefinition.Get.AddStatement("_value;");

            propertyDefinition.Set.LambdaSyntax = true;
            propertyDefinition.Set.AddStatement("_value = value;");

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
            var propertyDefinition = new PropertyDefinition("Test", TypeDefinition.Get(typeof(int)));
            
            propertyDefinition.Get.AddStatement("return _value;");
            
            propertyDefinition.Set.AddStatement("_value = value;");

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
    set
    {
        _value = value;
    }
}
";

        [Fact]
        public void PrivateStaticGetSetDefinition()
        {
            var propertyDefinition = new PropertyDefinition("Test", TypeDefinition.Get(typeof(int)));

            propertyDefinition.Modifiers |= ComponentModifier.Static | ComponentModifier.Private;

            propertyDefinition.Get.AddStatement("return _value;");

            propertyDefinition.Set.AddStatement("_value = value;");

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
            var propertyDefinition = new PropertyDefinition("Test", TypeDefinition.Get(typeof(int)));

            propertyDefinition.Modifiers |= ComponentModifier.Virtual | ComponentModifier.Protected;

            propertyDefinition.Get.AddStatement("return _value;");

            propertyDefinition.Set.AddStatement("_value = value;");

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
            var propertyDefinition = new PropertyDefinition("Test", TypeDefinition.Get(typeof(int)));

            propertyDefinition.Modifiers |= ComponentModifier.Override | ComponentModifier.Public;

            propertyDefinition.Get.AddStatement("return _value;");

            propertyDefinition.Set.AddStatement("_value = value;");

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
            var propertyDefinition = new PropertyDefinition("Test", TypeDefinition.Get(typeof(int)));

            propertyDefinition.IndexType = TypeDefinition.Get(typeof(string));

            propertyDefinition.Get.AddStatement("return _value;");

            propertyDefinition.Set.AddStatement("_value = value;");

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
