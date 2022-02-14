using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpAuthor
{
    public class StatementOutputComponent : BaseOutputComponent
    {
        private string statement;

        public StatementOutputComponent(string statement)
        {
            this.statement = statement;
        }

        public override void GetKnownTypes(List<TypeDefinition> types)
        {

        }

        public override void WriteOutput(IOutputContext outputContext)
        {
            outputContext.WriteIndentedLine(statement);
        }
    }
}
