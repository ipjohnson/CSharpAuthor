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

    public bool FileScopedNamespace
    {
        get => _namespaceDefinition.FileScopedNamespace;
        set => _namespaceDefinition.FileScopedNamespace = value;
    }

    public IEnumerable<IOutputComponent> GetAllNamedComponents() =>
        _namespaceDefinition.GetAllNamedComponents();

    public ClassDefinition AddClass(string name)
    {
        return _namespaceDefinition.AddClass(name);
    }

    public ClassDefinition AddRecord(string name)
    {
        return _namespaceDefinition.AddRecord(name);
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

    /// <summary>
    /// The namespace this file declares.
    /// </summary>
    public string Namespace => _namespaceDefinition.Namespace;

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        // The file's own namespace is in scope for everything in it, so a using naming it back is
        // noise. Said here rather than configured, because this is where it is known.
        if (outputContext is OutputContext context)
        {
            context.DeclareContainingNamespace(_namespaceDefinition.Namespace);
        }

        _namespaceDefinition.WriteOutput(outputContext);

        outputContext.GenerateUsingStatements();
    }
}