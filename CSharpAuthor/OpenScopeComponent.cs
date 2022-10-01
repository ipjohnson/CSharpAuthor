using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class OpenScopeComponent : BaseOutputComponent
{
    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.WriteIndentedLine("{");
        outputContext.IncrementIndent();
    }
}