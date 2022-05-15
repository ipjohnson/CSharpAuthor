using System;
using System.Collections.Generic;
using System.Linq;

namespace CSharpAuthor
{
    public class MethodDefinition : BaseOutputComponent
    {
        private readonly List<ParameterDefinition> parameters = new List<ParameterDefinition>();
        private readonly List<IOutputComponent> statements = new List<IOutputComponent>();
        
        private ITypeDefinition returnType;
        private int localVariableNameCount = 1;

        public MethodDefinition(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public ITypeDefinition ReturnType => returnType;

        public IReadOnlyList<ParameterDefinition> Parameters => parameters;

        public string NextLocalVariableName => "localVariable" + localVariableNameCount++;

        public MethodDefinition SetReturnType(Type type)
        {
            return SetReturnType(TypeDefinition.Get(type));
        }

        public MethodDefinition SetReturnType(ITypeDefinition type)
        {
            returnType = type;

            return this;
        }

        public ParameterDefinition AddParameter(Type type, string name)
        {
            return AddParameter(TypeDefinition.Get(type), name);
        }

        public ParameterDefinition AddParameter(ITypeDefinition typeDefinition, string name)
        {
            var parameter = new ParameterDefinition(typeDefinition, name);

            parameters.Add(parameter);

            return parameter;
        }

        public void NewLine()
        {
            AddStatement("");
        }

        public OpenScopeComponent OpenScope()
        {
            var openScope = new OpenScopeComponent();

            statements.Add(openScope);

            return openScope;
        }

        public CloseScopeComponent CloseScope()
        {
            var closeScope = new CloseScopeComponent();

            statements.Add(closeScope);

            return closeScope;
        }

        public StatementOutputComponent AddStatement(string statement, params object[] types)
        {
            var typeDefinitions = new List<ITypeDefinition>();

            if (types != null && types.Length > 0)
            {
                for (var index = 0; index < types.Length; index++)
                {
                    var typeSwapString = "{arg" + (index + 1) + "}";

                    var value = types[index];

                    if (value is Type typeValue)
                    {
                        value = TypeDefinition.Get(typeValue);
                    }

                    if (value is ITypeDefinition typeDefinition)
                    {
                        typeDefinitions.Add(typeDefinition);
                    }

                    statement = statement.Replace(typeSwapString, GetObjectStringValue(value));
                }
            }

            var statementOutput = new StatementOutputComponent(statement);

            statementOutput.AddTypes(typeDefinitions);

            statements.Add(statementOutput);
            
            return statementOutput;
        }
        
        
        public override void WriteOutput(IOutputContext outputContext)
        {
            ProcessNamespaces(outputContext);

            WriteMethodSignature(outputContext);

            WriteMethodBody(outputContext);
        }

        private string GetObjectStringValue(object value)
        {
            if (value is Type type)
            {
                value = TypeDefinition.Get(type);
            }

            if (value is ITypeDefinition typeDefinition)
            {
                return typeDefinition.GetShortName();
            }
            
            if (value is string stringValue)
            {
                return "\"" + stringValue + "\"";
            }

            return value.ToString();
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
            else if ((Modifiers & ComponentModifier.Abstract) == ComponentModifier.Abstract)
            {
                outputContext.Write(KeyWords.Abstract);
                outputContext.WriteSpace();
            }
            else if ((Modifiers & ComponentModifier.Virtual) == ComponentModifier.Virtual)
            {
                outputContext.Write(KeyWords.Virtual);
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
            if (returnType != null)
            {
                outputContext.Write(returnType);
            }
            else
            {
                outputContext.Write("void");
            }
        }
    }
}
