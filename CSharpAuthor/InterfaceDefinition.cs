using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class InterfaceDefinition : BaseOutputComponent
{
    protected readonly List<InterfacePropertyDefinition> Properties = new ();
    protected readonly List<InterfaceMethodDefinition> Methods = new ();
    protected readonly List<ITypeDefinition> BaseTypes = new ();

    public InterfaceDefinition(string name)
    {
        Name = name;
    }

    public string Name { get;  }

    public InterfaceDefinition AddBaseType(Type type)
    {
        return AddBaseType(TypeDefinition.Get(type));
    }
        
    public InterfaceDefinition AddBaseType(ITypeDefinition typeDefinition)
    {
        BaseTypes.Add(typeDefinition);

        return this;
    }

    public InterfacePropertyDefinition AddProperty(Type type, string name)
    {
        return AddProperty(TypeDefinition.Get(type), name);
    }
        
    public InterfacePropertyDefinition AddProperty(ITypeDefinition typeDefinition, string name)
    {
        var propertyDefinition = new InterfacePropertyDefinition(typeDefinition, name);

        Properties.Add(propertyDefinition);

        return propertyDefinition;
    }

    public InterfaceMethodDefinition AddMethod(string name)
    {
        var definition = new InterfaceMethodDefinition(name);

        Methods.Add(definition);

        return definition;
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        WriteInterfaceSignature(outputContext);
        WriteInterfaceOpening(outputContext);

        foreach (var methodDefinition in Methods)
        {
            methodDefinition.WriteOutput(outputContext);
        }

        foreach (var propertyDefinition in Properties)
        {
            propertyDefinition.WriteOutput(outputContext);
        }

        WriteInterfaceClosing(outputContext);
    }
        
    private void WriteInterfaceClosing(IOutputContext outputContext)
    {
        outputContext.CloseScope();
    }

    private void WriteInterfaceOpening(IOutputContext outputContext)
    {
        outputContext.OpenScope();
    }

    private void WriteInterfaceSignature(IOutputContext outputContext)
    {
        outputContext.Write(outputContext.IndentString);
        outputContext.Write(GetAccessModifier(KeyWords.Public));
        outputContext.WriteSpace();
            
        if ((Modifiers & ComponentModifier.Partial) == ComponentModifier.Partial)
        {
            outputContext.Write(KeyWords.Partial);
            outputContext.WriteSpace();
        }

        outputContext.Write(KeyWords.Interface);
        outputContext.WriteSpace();

        outputContext.Write(Name);

        if (BaseTypes.Count > 0)
        {
            outputContext.Write(" : ");

            BaseTypes.OutputCommaSeparatedList(outputContext);
        }

        outputContext.WriteLine();
    }
}