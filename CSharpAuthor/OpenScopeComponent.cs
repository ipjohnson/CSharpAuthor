using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class OpenScopeComponent : IOutputComponent
    {
        public void WriteOutput(IOutputContext outputContext)
        {
            outputContext.WriteIndentedLine("{");
            outputContext.IncrementIndent();
        }
    }
}
