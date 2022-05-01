using System;
using System.Collections.Generic;

namespace CSharpAuthor
{
    public class ClassDefinition : BaseOutputComponent
    {
        private readonly string _namespace;
        private readonly string _name;
        private readonly List<TypeDefinition> baseTypes = new List<TypeDefinition>();
        private readonly List<IOutputComponent> fields = new List<IOutputComponent>();
        private readonly List<IOutputComponent> constructors = new List<IOutputComponent>();
        private readonly List<IOutputComponent> methods = new List<IOutputComponent>();
        private readonly List<IOutputComponent> properties = new List<IOutputComponent>();

        public ClassDefinition(string ns, string name)
        {
            _namespace = ns;
            _name = name;
        }

        public FieldDefinition AddField(TypeDefinition typeDefinition, string fieldName)
        {
            var definition = new FieldDefinition(typeDefinition, fieldName);

            fields.Add(definition);

            return definition;
        }

        public MethodDefinition AddMethod(string method)
        {
            var definition = new MethodDefinition(method);

            methods.Add(definition);

            return definition;
        }

        public ClassDefinition AddBaseType(TypeDefinition typeDefinition)
        {
            baseTypes.Add(typeDefinition);

            return this;
        }

        public ConstructorDefinition AddConstructor()
        {
            var definition = new ConstructorDefinition(_name);

            constructors.Add(definition);

            return definition;
        }
        
        public override void WriteOutput(IOutputContext outputContext)
        {
            WriteNamespaceOpen(outputContext);

            WriteClassOpening(outputContext);

            ApplyAllComponents(component => component.WriteOutput(outputContext));

            WriteClassClosing(outputContext);

            WriteNamespaceClose(outputContext);

            outputContext.GenerateUsingStatements();
        }
        
        private void ApplyAllComponents(Action<IOutputComponent> componentAction)
        {
            foreach (var field in fields)
            {
                componentAction(field);
            }

            foreach (var constructor in constructors)
            {
                componentAction(constructor);
            }

            foreach (var method in methods)
            {                
                componentAction(method);
            }

            foreach (var property in properties)
            {
                componentAction(property);
            }
        }

        private void WriteClassClosing(IOutputContext outputContext)
        {
            outputContext.CloseScope();
        }

        private void WriteClassOpening(IOutputContext outputContext)
        {
            WriteClassSignature(outputContext);
            outputContext.OpenScope();
        }

        private void WriteClassSignature(IOutputContext outputContext)
        {
            outputContext.Write(outputContext.IndentString);
            outputContext.Write(GetAccessModifier(KeyWords.Public));
            outputContext.WriteSpace();

            outputContext.Write(GetAccessModifier(KeyWords.Class));
            outputContext.WriteSpace();

            if ((Modifiers & ComponentModifier.Static) == ComponentModifier.Static)
            {
                outputContext.Write(KeyWords.Static);
                outputContext.WriteSpace();
            }

            outputContext.Write(_name);

            if (baseTypes.Count > 0)
            {
                outputContext.Write(" : ");

                for (var i = 0; i < baseTypes.Count; i++)
                {
                    if (i > 0)
                    {
                        outputContext.Write(", ");
                    }

                    var type = baseTypes[i];

                    outputContext.Write(type.Name);
                }
            }

            outputContext.WriteLine();
        }

        private void WriteNamespaceClose(IOutputContext outputContext)
        {
            outputContext.CloseScope();
        }

        private void WriteNamespaceOpen(IOutputContext outputContext)
        {
            outputContext.WriteIndentedLine("namespace " + _namespace);
            outputContext.OpenScope();
        }
    }
}
