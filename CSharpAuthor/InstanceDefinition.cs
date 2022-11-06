using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpAuthor;

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
        
    public InvokeGenericDefinition InvokeGeneric(string methodName, IEnumerable<ITypeDefinition> genericArgs, params object[] parameters)
    {
        var invokeDefinition = 
            new InvokeGenericDefinition(Name, methodName, genericArgs.ToList()) { Indented = false };

        foreach (var parameter in parameters)
        {
            invokeDefinition.AddArgument(parameter);
        }

        return invokeDefinition;
    }

    public InstanceDefinition Property(string propertyName)
    {
        return new InstanceDefinition(Name + "." + propertyName);
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.Write(Name);
    }
}