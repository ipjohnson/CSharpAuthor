using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class InterfaceMethodDefinition : MethodDefinition
    {
        public InterfaceMethodDefinition(string name) : base(name)
        {
            
        }

        protected override void WriteMethodBody(IOutputContext outputContext)
        {
            if (StatementCount > 0)
            {
                base.WriteMethodBody(outputContext);
            }
        }

        protected override void WriteEndOfMethodSignature(IOutputContext outputContext)
        {
            if (StatementCount == 0)
            {
                outputContext.Write(";");
            }

            outputContext.WriteLine();
        }
        

        protected override void WriteAccessModifier(IOutputContext outputContext)
        {
            outputContext.WriteIndent();
        }
    }
}
