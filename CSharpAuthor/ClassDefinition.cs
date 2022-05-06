﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace CSharpAuthor
{
    public class ClassDefinition : BaseOutputComponent
    {
        private readonly string _namespace;
        private readonly string _name;
        private readonly List<TypeDefinition> _baseTypes = new List<TypeDefinition>();
        private readonly List<FieldDefinition> _fields = new List<FieldDefinition>();
        private readonly List<IOutputComponent> _constructors = new List<IOutputComponent>();
        private readonly List<MethodDefinition> _methods = new List<MethodDefinition>();
        private readonly List<IOutputComponent> _properties = new List<IOutputComponent>();

        public ClassDefinition(string ns, string name)
        {
            _namespace = ns;
            _name = name;
        }

        public int FieldCount => _fields.Count;

        public FieldDefinition AddField(TypeDefinition typeDefinition, string fieldName)
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

        public ClassDefinition AddBaseType(TypeDefinition typeDefinition)
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

            ApplyAllComponents(component => component.WriteOutput(outputContext));

            WriteClassClosing(outputContext);

            WriteNamespaceClose(outputContext);

            outputContext.GenerateUsingStatements();
        }
        
        private void ApplyAllComponents(Action<IOutputComponent> componentAction)
        {
            foreach (var field in _fields)
            {
                componentAction(field);
            }

            foreach (var constructor in _constructors)
            {
                componentAction(constructor);
            }

            foreach (var method in _methods)
            {                
                componentAction(method);
            }

            foreach (var property in _properties)
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
