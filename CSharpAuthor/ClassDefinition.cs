using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;

namespace CSharpAuthor
{
    public class ClassDefinition : BaseOutputComponent
    {
        private readonly string _namespace;
        private readonly string _name;
        private readonly List<ITypeDefinition> _baseTypes = new List<ITypeDefinition>();
        private readonly List<FieldDefinition> _fields = new List<FieldDefinition>();
        private readonly List<ConstructorDefinition> _constructors = new List<ConstructorDefinition>();
        private readonly List<MethodDefinition> _methods = new List<MethodDefinition>();
        private readonly List<PropertyDefinition> _properties = new List<PropertyDefinition>();

        public ClassDefinition(string ns, string name)
        {
            _namespace = ns;
            _name = name;
        }

        public int FieldCount => _fields.Count;

        public IReadOnlyList<ConstructorDefinition> Constructors => _constructors;

        public IReadOnlyList<MethodDefinition> Methods => _methods;

        public IReadOnlyList<IOutputComponent> Properties => _properties;
        
        public FieldDefinition AddField(Type type, string fieldName)
        {
            return AddField(TypeDefinition.Get(type), fieldName);
        }

        public FieldDefinition AddField(ITypeDefinition typeDefinition, string fieldName)
        {
            if (_fields.Any(f => f.Name == fieldName))
            {
                throw new ArgumentException($"{fieldName} field already exists in class");
            }

            var definition = new FieldDefinition(typeDefinition, fieldName);

            _fields.Add(definition);

            return definition;
        }

        public MethodDefinition AddMethod(string method)
        {
            var definition = new MethodDefinition(method);

            _methods.Add(definition);

            return definition;
        }

        public ClassDefinition AddBaseType(ITypeDefinition typeDefinition)
        {
            if (!_baseTypes.Contains(typeDefinition))
            {
                _baseTypes.Add(typeDefinition);
            }

            return this;
        }

        public ConstructorDefinition AddConstructor()
        {
            var definition = new ConstructorDefinition(_name);

            _constructors.Add(definition);

            return definition;
        }
        
        public override void WriteOutput(IOutputContext outputContext)
        {
            WriteNamespaceOpen(outputContext);

            WriteClassOpening(outputContext);

            ApplyAllComponents(component => component.WriteOutput(outputContext), outputContext);

            WriteClassClosing(outputContext);

            WriteNamespaceClose(outputContext);

            outputContext.GenerateUsingStatements();
        }
        
        private void ApplyAllComponents(Action<IOutputComponent> componentAction, IOutputContext outputContext)
        {
            foreach (var field in _fields)
            {
                componentAction(field);
            }
            
            foreach (var constructor in _constructors)
            {
                outputContext.WriteLine();

                componentAction(constructor);
            }

            foreach (var method in _methods)
            {
                outputContext.WriteLine();

                componentAction(method);
            }

            foreach (var property in _properties)
            {
                outputContext.WriteLine();

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
            else if ((Modifiers & ComponentModifier.Abstract) == ComponentModifier.Abstract)
            {
                outputContext.Write(KeyWords.Abstract);
                outputContext.WriteSpace();
            }

            if ((Modifiers & ComponentModifier.Partial) == ComponentModifier.Partial)
            {
                outputContext.Write(KeyWords.Partial);
                outputContext.WriteSpace();
            }

            outputContext.Write(_name);

            if (_baseTypes.Count > 0)
            {
                outputContext.Write(" : ");

                for (var i = 0; i < _baseTypes.Count; i++)
                {
                    if (i > 0)
                    {
                        outputContext.Write(", ");
                    }

                    var type = _baseTypes[i];

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
