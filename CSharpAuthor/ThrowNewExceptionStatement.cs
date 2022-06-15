using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpAuthor
{
    internal class ThrowNewExceptionStatement : BaseOutputComponent
    {
        private readonly ITypeDefinition _exceptionType;
        private readonly IReadOnlyList<IOutputComponent> _parameters;

        public ThrowNewExceptionStatement(ITypeDefinition exceptionType, object[] parameters)
        {
            _exceptionType = exceptionType;
            _parameters = CodeOutputComponent.GetAll(parameters).ToList();
        }

        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            outputContext.WriteIndent("throw new ");
            outputContext.Write(_exceptionType);
            outputContext.Write("(");
            _parameters.OutputCommaSeparatedList(outputContext);
            outputContext.WriteLine(");");
        }
    }
}
