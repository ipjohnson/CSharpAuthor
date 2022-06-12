using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class ForEachDefinition : BaseBlockDefinition
    {
        private readonly IOutputComponent _foreachStatement;

        public ForEachDefinition(IOutputComponent foreachStatement)
        {
            _foreachStatement = foreachStatement;
        }

        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            outputContext.WriteIndent("foreach(");
            _foreachStatement.WriteOutput(outputContext);
            outputContext.WriteLine(")");
            
            WriteBlock(outputContext);
        }
    }
}
