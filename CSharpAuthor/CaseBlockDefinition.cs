using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class CaseBlockDefinition : BaseBlockDefinition
    {
        private readonly IOutputComponent _caseStatement;

        public CaseBlockDefinition(IOutputComponent caseStatement)
        {
            _caseStatement = caseStatement;
        }
        
        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            outputContext.WriteIndent();
            _caseStatement.WriteOutput(outputContext);
            outputContext.WriteLine();

            outputContext.IncrementIndent();

            foreach (var caseBlockDefinition in StatementList)
            {
                caseBlockDefinition.WriteOutput(outputContext);
            }

            outputContext.DecrementIndent();
        }
    }
}
