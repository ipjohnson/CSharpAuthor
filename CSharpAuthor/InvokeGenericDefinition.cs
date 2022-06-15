using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class InvokeGenericDefinition : BaseOutputComponent
    {
        private readonly string _instance;
        private readonly string _methodName;
        private readonly IReadOnlyList<ITypeDefinition> _genericArguments;
        private readonly List<IOutputComponent> _arguments = new();

        public InvokeGenericDefinition(string instance, string methodName, IReadOnlyList<ITypeDefinition> genericArguments) 
        {
            _instance = instance;
            _methodName = methodName;
            _genericArguments = genericArguments;
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

            outputContext.Write(_instance);
            outputContext.Write(".");
            outputContext.Write(_methodName);
            outputContext.Write("<");
            _genericArguments.OutputCommaSeparatedList(outputContext);
            outputContext.Write(">");
            outputContext.Write("(");

            _arguments.OutputCommaSeparatedList(outputContext);

            outputContext.Write(")");
        }
    }
}
