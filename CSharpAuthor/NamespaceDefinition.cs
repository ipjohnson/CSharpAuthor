using System.Collections.Generic;
using System.Linq;

namespace CSharpAuthor;

/// <summary>
/// A namespace declaration and the types in it.
/// </summary>
/// <remarks>
/// A <see cref="CSharpFileDefinition"/> holds one of these and forwards its <c>Add*</c> methods to
/// it, so a caller building an ordinary file never sees this. It is reached directly for a file
/// that declares more than one namespace, or a nested one - see <see cref="AddNamespace"/>.
/// </remarks>
public class NamespaceDefinition : BaseOutputComponent, IConstructContainer
{
    private readonly string _namespace;
    private readonly List<IOutputComponent> _outputComponents = new ();

    /// <summary>
    /// Writes <c>namespace Sample;</c> rather than wrapping the contents in braces, which saves
    /// every declaration one level of indent. C# 10.
    /// </summary>
    /// <remarks>
    /// Only legal on the one namespace a file declares, so setting it on a nested
    /// <see cref="NamespaceDefinition"/> produces a file that does not compile.
    /// </remarks>
    public bool FileScopedNamespace { get; set; }

    /// <summary>
    /// A namespace declaring <paramref name="ns"/>. The empty default writes no declaration at all
    /// and puts its contents at the file's top level.
    /// </summary>
    public NamespaceDefinition(string ns = "")
    {
        _namespace = ns;
    }

    /// <summary>
    /// The namespace this definition declares, relative to its parent.
    /// </summary>
    public string Namespace => _namespace;

    /// <summary>
    /// Returns the nested namespace with this name, adding it if it is not already there.
    /// </summary>
    /// <remarks>
    /// Reuse rather than append, because a file is usually built by several independent pieces of
    /// code and more than one of them will want the same namespace. Appending a second definition
    /// gave a file with two <c>namespace Models { }</c> blocks - which compiles, and is not what
    /// anyone meant. Callers that want a distinct block can still construct a
    /// <see cref="NamespaceDefinition"/> and pass it to <see cref="AddComponent"/>.
    /// </remarks>
    public NamespaceDefinition AddNamespace(string @namespace)
    {
        foreach (var outputComponent in _outputComponents)
        {
            if (outputComponent is NamespaceDefinition existing &&
                string.Equals(existing._namespace, @namespace, System.StringComparison.Ordinal))
            {
                return existing;
            }
        }

        var namespaceDefinition = new NamespaceDefinition(@namespace);

        _outputComponents.Add(namespaceDefinition);

        return namespaceDefinition;
    }
    
    /// <summary>
    /// The declarations in this namespace that have a name, for inspecting a tree built elsewhere.
    /// </summary>
    public IEnumerable<IOutputComponent> GetAllNamedComponents()
    {
        return _outputComponents.Where(x => x is INamedComponent);
    }

    /// <inheritdoc cref="CSharpFileDefinition.AddClass" />
    public ClassDefinition AddClass(string name)
    {
        var classDefinition = new ClassDefinition(name);

        _outputComponents.Add(classDefinition);

        return classDefinition;
    }

    /// <inheritdoc cref="CSharpFileDefinition.AddRecord" />
    public ClassDefinition AddRecord(string name)
    {
        var classDefinition = new ClassDefinition(name) { TypeKeyword = ClassKeyword.Record };

        _outputComponents.Add(classDefinition);

        return classDefinition;
    }

    /// <inheritdoc cref="CSharpFileDefinition.AddEnum" />
    public EnumDefinition AddEnum(string name)
    {
        var enumDefinition = new EnumDefinition(name);

        _outputComponents.Add(enumDefinition);

        return enumDefinition;
    }

    /// <inheritdoc cref="CSharpFileDefinition.AddInterface" />
    public InterfaceDefinition AddInterface(string interfaceName)
    {
        var interfaceDefinition = new InterfaceDefinition(interfaceName);

        _outputComponents.Add(interfaceDefinition);

        return interfaceDefinition;
    }

    /// <inheritdoc cref="CSharpFileDefinition.AddComponent" />
    public void AddComponent(IOutputComponent component)
    {
        _outputComponents.Add(component);
    }
    

    /// <summary>
    /// A documentation comment on the namespace declaration.
    /// </summary>
    /// <remarks>
    /// <see cref="BaseOutputComponent.Comment"/> is settable on every component, and this was one
    /// of the two that never read it - so a comment set on a namespace compiled, read as documented,
    /// and emitted nothing.
    /// </remarks>
    protected override void WriteComment(IOutputContext outputContext)
    {
        DocumentationComment.WriteSummary(outputContext.WriteIndentedLine, Comment);
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        if (!string.IsNullOrEmpty(_namespace))
        {
            if (FileScopedNamespace)
            {
                outputContext.WriteIndentedLine(
                    "namespace " + CSharpIdentifier.EscapeQualified(_namespace) + ";");
                outputContext.WriteLine();
            }
            else
            {
                WriteNamespaceOpen(outputContext);
            }
        }

        var newLine = false;

        foreach (var outputComponent in _outputComponents)
        {
            if (newLine)
            {
                outputContext.WriteLine();
            }
            else
            {
                newLine = true;
            }

            outputComponent.WriteOutput(outputContext);
        }

        if (!string.IsNullOrEmpty(_namespace) && !FileScopedNamespace)
        {
            WriteNamespaceClose(outputContext);
        }
    }

    private void WriteNamespaceOpen(IOutputContext outputContext)
    {
        outputContext.WriteIndentedLine(
            "namespace " + CSharpIdentifier.EscapeQualified(_namespace));
        outputContext.OpenScope();
    }

    private void WriteNamespaceClose(IOutputContext outputContext)
    {
        outputContext.CloseScope();
    }
}