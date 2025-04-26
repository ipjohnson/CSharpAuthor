using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class CSharpFileDefinition : BaseOutputComponent, IConstructContainer
{
    private readonly NamespaceDefinition _namespaceDefinition;

    public CSharpFileDefinition(string ns = "")
    {
        _namespaceDefinition = new NamespaceDefinition(ns);
    }

    public ClassDefinition AddClass(string name)
    {
        return _namespaceDefinition.AddClass(name);
    }

    public EnumDefinition AddEnum(string name)
    {
        return _namespaceDefinition.AddEnum(name);
    }

    public InterfaceDefinition AddInterface(string interfaceName)
    {
        return _namespaceDefinition.AddInterface(interfaceName);
    }

    public void AddComponent(IOutputComponent component)
    {
        _namespaceDefinition.AddComponent(component);
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        _namespaceDefinition.WriteOutput(outputContext);

        outputContext.GenerateUsingStatements();
    }
}