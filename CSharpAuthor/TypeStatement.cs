using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class TypeStatement : BaseOutputComponent
    {
        private readonly ITypeDefinition _typeDefinition;

        public TypeStatement(ITypeDefinition typeDefinition)
        {
            _typeDefinition = typeDefinition;
        }

        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            outputContext.Write(_typeDefinition);
        }
    }
}
