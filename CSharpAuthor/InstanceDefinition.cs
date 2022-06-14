using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class InstanceDefinition : BaseOutputComponent
    {
        public InstanceDefinition(string name)
        {
            Name = name;
        }
        
        public string Name { get; }

        public InvokeDefinition Invoke(string methodName, params object[] parameters)
        {
            var invokeDefinition = new InvokeDefinition(Name, methodName) { Indented = false };

            foreach (var parameter in parameters)
            {
                invokeDefinition.AddArgument(parameter);
            }

            return invokeDefinition;
        }

        public IOutputComponent Property(string propertyName)
        {
            return new InstanceDefinition(Name + "." + propertyName);
        }

        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            outputContext.Write(Name);
        }
    }
}
