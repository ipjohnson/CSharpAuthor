using System;
using System.Collections.Generic;
using System.Linq;

namespace CSharpAuthor
{
    public class MethodDefinition : BaseOutputComponent
    {
        protected readonly List<ParameterDefinition> ParameterList = new ();
        protected readonly List<IOutputComponent> StatementList = new ();
        
        private ITypeDefinition? _returnType;
        private int _localVariableNameCount = 1;

        public MethodDefinition(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public ITypeDefinition? ReturnType => _returnType;

        public IReadOnlyList<ParameterDefinition> Parameters => ParameterList;

        public string NextLocalVariableName => "localVariable" + _localVariableNameCount++;

        public int StatementCount => StatementList.Count;

        public MethodDefinition SetReturnType(Type type)
        {
            return SetReturnType(TypeDefinition.Get(type));
        }

        public MethodDefinition SetReturnType(ITypeDefinition type)
        {
            _returnType = type;

            return this;
        }

        public ParameterDefinition AddParameter(Type type, string name)
        {
            return AddParameter(TypeDefinition.Get(type), name);
        }

        public ParameterDefinition AddParameter(ITypeDefinition typeDefinition, string name)
        {
            var parameter = new ParameterDefinition(typeDefinition, name);

            ParameterList.Add(parameter);

            return parameter;
        }

        public virtual void NewLine()
        {
            AddStatement("");
        }

        public virtual OpenScopeComponent OpenScope()
        {
            var openScope = new OpenScopeComponent();

            StatementList.Add(openScope);

            return openScope;
        }

        public virtual CloseScopeComponent CloseScope()
        {
            var closeScope = new CloseScopeComponent();

            StatementList.Add(closeScope);

            return closeScope;
        }

        public virtual StatementOutputComponent AddStatement(string statement, params object[] types)
        {
            var typeDefinitions = new List<ITypeDefinition>();

            if (types != null && types.Length > 0)
            {
                for (var index = 0; index < types.Length; index++)
                {
                    var value = types[index];
                    var typeSwapString = "{arg" + (index + 1) + "}";

                    if (statement.IndexOf(typeSwapString, StringComparison.CurrentCulture) >= 0)
                    {

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
                    else
                    {
                        var rawSwapString = $"[arg{index + 1}]";

                        if (statement.IndexOf(rawSwapString, StringComparison.CurrentCulture) >= 0)
                        {
                            statement = statement.Replace(rawSwapString, value.ToString());
                        }
                    }
                }
            }

            var statementOutput = new StatementOutputComponent(statement);

            statementOutput.AddTypes(typeDefinitions);

            StatementList.Add(statementOutput);
            
            return statementOutput;
        }
        
        protected override void WriteComponentOutput(IOutputContext outputContext)
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
            if (_returnType != null)
            {
                outputContext.AddImportNamespace(_returnType);
            }
        }

        protected virtual void WriteMethodBody(IOutputContext outputContext)
        {
            outputContext.OpenScope();

            foreach (var outputComponent in StatementList)
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

            for (var i = 0; i < ParameterList.Count; i++)
            {
                if (i > 0)
                {
                    outputContext.Write(", ");
                }

                ParameterList[i].WriteOutput(outputContext);
            }

            outputContext.Write(")");

            WriteEndOfMethodSignature(outputContext);
        }

        protected virtual void WriteEndOfMethodSignature(IOutputContext outputContext)
        {
            outputContext.WriteLine();
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
            else if ((Modifiers & ComponentModifier.Override) == ComponentModifier.Override)
            {
                outputContext.Write(KeyWords.Override);
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
            if (_returnType != null)
            {
                outputContext.Write(_returnType);
            }
            else
            {
                outputContext.Write("void");
            }
        }
    }
}
