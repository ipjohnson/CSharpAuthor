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

    /// <summary>
    /// A file-level documentation comment, written above the using directives.
    /// </summary>
    /// <remarks>
    /// <see cref="BaseOutputComponent.Comment"/> is settable on every component and was read by
    /// most of them; this one and <see cref="NamespaceDefinition"/> never read it, so a comment set
    /// on a file compiled, read as documented and emitted nothing at all.
    /// </remarks>
    protected override void WriteComment(IOutputContext outputContext)
    {
        DocumentationComment.WriteSummary(outputContext.WriteIndentedLine, Comment);
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        // The file's own namespace is in scope for everything in it, so a using naming it back is
        // noise. Said here rather than configured, because this is where it is known.
        if (outputContext is OutputContext context)
        {
            context.DeclareContainingNamespace(_namespaceDefinition.Namespace);

            // Anything written before this point - a leading trait, the file's own comment - is the
            // header, and the generated using directives go after it rather than above it.
            context.MarkEndOfFileHeader();
        }

        _namespaceDefinition.WriteOutput(outputContext);

        outputContext.GenerateUsingStatements();
    }
}