using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpAuthor
{
    public class FieldDefinition : BaseOutputComponent
    {
        private readonly TypeDefinition _typeDefinition;
        private readonly string _name;

        public FieldDefinition(TypeDefinition typeDefinition, string name)
        {
            _typeDefinition = typeDefinition;
            _name = name;
        }

        public override void GetKnownTypes(List<TypeDefinition> types)
        {

        }

        public override void WriteOutput(IOutputContext outputContext)
        {

        }
    }
}
