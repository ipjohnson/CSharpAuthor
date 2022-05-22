using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class CloseScopeComponent : BaseOutputComponent
    {
        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            outputContext.DecrementIndent();
            outputContext.WriteIndentedLine("}");
        }
    }
}
