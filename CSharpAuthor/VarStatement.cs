using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class VarStatement : BaseOutputComponent
    {
        private readonly IOutputComponent _variable;

        public VarStatement(IOutputComponent variable)
        {
            _variable = variable;
        }

        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            if (Indented)
            {
                outputContext.WriteIndent();
            }

            outputContext.Write("var ");
            
            _variable.WriteOutput(outputContext);
        }
    }
}
