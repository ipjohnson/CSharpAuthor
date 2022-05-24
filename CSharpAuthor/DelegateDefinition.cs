using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class DelegateDefinition : MethodDefinition
    {
        public DelegateDefinition(string name) : base(name)
        {
        }

        protected override void WriteAccessModifier(IOutputContext outputContext)
        {
            outputContext.WriteIndent();
            outputContext.Write(GetAccessModifier(KeyWords.Public));
            outputContext.WriteSpace();
            outputContext.Write("delegate");
            outputContext.WriteSpace();
        }

        protected override void WriteEndOfMethodSignature(IOutputContext outputContext)
        {
            outputContext.WriteLine(";");
        }

        protected override void WriteMethodBody(IOutputContext outputContext)
        {

        }
    }
}
