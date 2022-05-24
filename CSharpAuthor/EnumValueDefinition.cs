using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class EnumValueDefinition : BaseOutputComponent
    {
        private readonly string _enumValueName;

        public EnumValueDefinition(string enumValueName)
        {
            _enumValueName = enumValueName;
        }

        public object Value { get; set; }

        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            outputContext.WriteIndent();
            outputContext.Write(_enumValueName);

            if (Value != null)
            {
                outputContext.Write(" = ");
                outputContext.Write(Value.ToString());
            }

            outputContext.WriteLine(",");
        }
    }
}
