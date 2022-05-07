using System;
using System.Collections.Generic;

namespace CSharpAuthor
{
    public class MethodDefinition : BaseOutputComponent
    {
        private readonly List<ParameterDefinition> parameters = new List<ParameterDefinition>();
        private readonly List<IOutputComponent> statements = new List<IOutputComponent>();
        
        private ITypeDefinition returnType;

        public MethodDefinition(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public MethodDefinition SetReturnType(Type type)
        {
            return SetReturnType(TypeDefinition.Get(type));
        }

        public MethodDefinition SetReturnType(ITypeDefinition type)
        {
            returnType = type;

            return this;
        }

        public ParameterDefinition AddParameter(TypeDefinition typeDefinition, string name)
        {
            var parameter = new ParameterDefinition(typeDefinition, name);

            parameters.Add(parameter);

            return parameter;
        }
        
        public StatementOutputComponent AddStatement(string statement)
        {
            var statementOutput = new StatementOutputComponent(statement);

            statements.Add(statementOutput);

            return statementOutput;
        }
        
        public override void WriteOutput(IOutputContext outputContext)
        {
            ProcessNamespaces(outputContext);

            WriteMethodSignature(outputContext);

            WriteMethodBody(outputContext);
        }

        private void ProcessNamespaces(IOutputContext outputContext)
        {
            if (returnType != null)
            {
                outputContext.AddImportNamespace(returnType);
            }
        }

        private void WriteMethodBody(IOutputContext outputContext)
        {
            outputContext.OpenScope();

            foreach (var outputComponent in statements)
            {
                outputComponent.WriteOutput(outputContext);
            }
            
            outputContext.CloseScope();
        }

        protected virtual void WriteMethodSignature(IOutputContext outputContext)
        {
            WriteAccessModifier(outputContext);

            WriteReturnType(outputContext);

            outputContext.WriteSpace();

            outputContext.Write(Name);

            outputContext.Write("(");

            for (var i = 0; i < parameters.Count; i++)
            {
                if (i > 0)
                {
                    outputContext.Write(", ");
                }

                parameters[i].WriteOutput(outputContext);
            }

            outputContext.WriteLine(")");
        }

        protected virtual void WriteAccessModifier(IOutputContext outputContext)
        {
            outputContext.WriteIndent();
            outputContext.Write(GetAccessModifier(KeyWords.Public));
            outputContext.WriteSpace();

            if ((Modifiers & ComponentModifier.Static) == ComponentModifier.Static)
            {
                outputContext.Write(KeyWords.Static);
                outputContext.WriteSpace();
            }

            if ((Modifiers & ComponentModifier.Async) == ComponentModifier.Async)
            {
                outputContext.Write(KeyWords.Async);
                outputContext.WriteSpace();
            }
        }

        protected virtual void WriteReturnType(IOutputContext outputContext)
        {
            outputContext.Write(returnType != null ? returnType.Name : "void");
        }
    }
}
