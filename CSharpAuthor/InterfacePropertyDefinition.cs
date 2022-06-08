using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class InterfacePropertyDefinition : PropertyDefinition
    {
        public InterfacePropertyDefinition(ITypeDefinition typeDefinition, string name) : base(typeDefinition, name)
        {

        }

        protected override void WriteAccessModifiers(IOutputContext outputContext)
        {
            outputContext.WriteIndent();
        }
    }
}
