using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpAuthor
{
    public class ParameterDefinition : BaseOutputComponent
    {
        private TypeDefinition typeDefinition;
        private string name;

        public ParameterDefinition(TypeDefinition typeDefinition, string name)
        {
            this.typeDefinition = typeDefinition;
            this.name = name;
        }

        public override void GetKnownTypes(List<TypeDefinition> types)
        {
            types.Add(typeDefinition);
        }

        public override void WriteOutput(IOutputContext outputContext)
        {

        }
    }
}
