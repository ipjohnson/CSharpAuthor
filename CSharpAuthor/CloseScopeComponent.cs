using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class CloseScopeComponent : IOutputComponent
    {
        public void WriteOutput(IOutputContext outputContext)
        {
            outputContext.DecrementIndent();
            outputContext.WriteIndentedLine("}");
        }
    }
}
