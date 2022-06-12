using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    internal class ThrowNewExceptionStatement : BaseOutputComponent
    {
        private readonly ITypeDefinition exceptionType;
        private readonly object[] parameters;

        public ThrowNewExceptionStatement(ITypeDefinition exceptionType, object[] parameters)
        {
            this.exceptionType = exceptionType;
            this.parameters = parameters;
        }

        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            outputContext.WriteIndent("throw new ");
            outputContext.Write(exceptionType);
            outputContext.Write("(");

            var comma = false;

            foreach (var parameter in parameters)
            {
                if (comma)
                {
                    outputContext.Write(", ");
                }
                else
                {
                    comma = true;
                }

                outputContext.Write(parameter.ToString());
            }

            outputContext.WriteLine(");");
        }
    }
}
