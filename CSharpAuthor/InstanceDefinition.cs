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

    /// <summary>
    /// The instance, by name.
    /// </summary>
    /// <remarks>
    /// Each dotted segment is escaped, because <see cref="Property"/> builds <c>a.b.c</c> into one
    /// name. <c>this</c> and <c>base</c> are left alone - they arrive here as expressions rather
    /// than as names, and <c>@this</c> would not mean the same thing.
    /// </remarks>
    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.Write(CSharpIdentifier.EscapeReference(Name));
    }
}