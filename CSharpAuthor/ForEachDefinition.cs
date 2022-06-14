using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class ForEachDefinition : BaseBlockDefinition
    {
        private readonly IOutputComponent _enumerableStatement;

        public ForEachDefinition(string instanceName, IOutputComponent enumerableStatement)
        {
            _enumerableStatement = enumerableStatement;
            Instance = new InstanceDefinition(instanceName);
        }

        public InstanceDefinition Instance { get; }

        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            outputContext.WriteIndent("foreach(var ");
            Instance.WriteOutput(outputContext);
            outputContext.Write(" in ");
            _enumerableStatement.WriteOutput(outputContext);
            outputContext.WriteLine(")");
            
            WriteBlock(outputContext);
        }
    }
}
