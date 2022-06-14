using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class NewStatement : InstanceDefinition
    {
        private readonly ITypeDefinition _typeDefinition;
        private readonly List<IOutputComponent> _arguments = new ();

        public NewStatement(ITypeDefinition typeDefinition, params object[] arguments) : base("")
        {
            _typeDefinition = typeDefinition;

            foreach (var argument in arguments)
            {
                _arguments.Add(StatementOutputComponent.Get(argument));
            }
        }
        
        public void AddArgument(object argument)
        {
            AddArgument(StatementOutputComponent.Get(argument));
        }

        public void AddArgument(IOutputComponent argument)
        {
            _arguments.Add(argument);
        }
        
        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            outputContext.Write("new ");
            outputContext.Write(_typeDefinition);
            outputContext.Write("(");
            
            for (var i = 0; i < _arguments.Count; i++)
            {
                if (i > 0)
                {
                    outputContext.Write(", ");
                }
                _arguments[i].WriteOutput(outputContext);
            }

            outputContext.Write(")");
        }
    }
}
