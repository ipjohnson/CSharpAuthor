using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class InvokeDefinition : BaseOutputComponent
    {
        private readonly string _instance;
        private readonly string _methodName;
        private readonly List<IOutputComponent> _arguments = new ();

        public InvokeDefinition(string instance, string methodName, params object[] arguments)
        {
            _instance = instance;
            _methodName = methodName;

            foreach (var argument in arguments)
            {
                AddArgument(argument);
            }
        }

        public void AddArgument(object argument)
        {
            AddArgument(CodeOutputComponent.Get(argument));
        }

        public void AddArgument(IOutputComponent argument)
        {
            _arguments.Add(argument);
        }

        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            if (Indented)
            {
                outputContext.WriteIndent();
            }

            if (!string.IsNullOrEmpty(_instance))
            {
                outputContext.Write(_instance);
                outputContext.Write(".");
            }

            outputContext.Write(_methodName);
            outputContext.Write("(");

            _arguments.OutputCommaSeparatedList(outputContext);

            outputContext.Write(")");
        }
    }
}
