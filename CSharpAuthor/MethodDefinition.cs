using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpAuthor
{
    public class MethodDefinition : BaseOutputComponent
    {
        private readonly List<ParameterDefinition> parameters = new ();
        private readonly List<IOutputComponent> statements = new ();
        private readonly string name;
        private TypeDefinition? returnType;

        public MethodDefinition(string name)
        {
            this.name = name;
        }

        public MethodDefinition SetReturnType(TypeDefinition type)
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

        public override void GetKnownTypes(List<TypeDefinition> types)
        {
            foreach (var parameter in parameters)
            {
                parameter.GetKnownTypes(types);
            }

            foreach (var statement in statements)
            {
                statement.GetKnownTypes(types);
            }
        }

        public override void WriteOutput(IOutputContext outputContext)
        {
            WriteMethodSignature(outputContext);

            WriteMethodBody(outputContext);
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

        private void WriteMethodSignature(IOutputContext outputContext)
        {
            outputContext.Write(GetAccessModifier(KeyWords.Private));
            outputContext.WriteSpace();

            outputContext.Write(returnType != null ? returnType.Name : "void");

            outputContext.WriteSpace();

            outputContext.Write(name);

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
    }
}
