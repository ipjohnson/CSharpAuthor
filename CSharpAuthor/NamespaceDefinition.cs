using System;
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
    private List<string>? _innerUsings;

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
    /// Writes <c>using <paramref name="ns"/>;</c> inside this namespace declaration, before its
    /// first member, rather than at the top of the file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A using inside a namespace is consulted before every file-level one, so the same directive
    /// binds names differently in the two places. This is for a generator that copies user code and
    /// has to repeat each using at the nesting level the source declared it at. For a using that
    /// only has to be somewhere in the file, call <see cref="BaseOutputComponent.AddUsingNamespace"/>,
    /// which this class also inherits and which still means file level.
    /// </para>
    /// <para>
    /// The name is written as given, keyword segments escaped with <c>@</c>, and resolved by the
    /// compiler relative to the enclosing namespace - which is what a copied directive needs. It is
    /// written in every <see cref="TypeOutputMode"/>, because an extension method is reached through
    /// a using and no other way.
    /// </para>
    /// <para>
    /// The library does not know what the named namespace contains, so in
    /// <see cref="TypeOutputMode.ShortName"/> a type written inside this declaration is written
    /// <c>global::</c>-qualified unless the declaration itself, or one nested in it, is the
    /// type's namespace. Otherwise a type of the same name in <paramref name="ns"/> would capture
    /// it, and the file would compile against the wrong type.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="ns"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">
    /// This namespace has no name, so it writes no declaration to put the directive in.
    /// </exception>
    public void AddUsingInsideNamespace(string ns)
    {
        if (string.IsNullOrEmpty(ns))
        {
            throw new ArgumentException("A using directive needs a namespace name.", nameof(ns));
        }

        if (string.IsNullOrEmpty(_namespace))
        {
            throw new InvalidOperationException(
                "A namespace with no name writes no declaration to put a using in. " +
                "Call AddUsingNamespace for a file-level using.");
        }

        _innerUsings ??= new List<string>();

        foreach (var existing in _innerUsings)
        {
            if (string.Equals(existing, ns, StringComparison.Ordinal))
            {
                return;
            }
        }

        _innerUsings.Add(ns);
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
        var declares = !string.IsNullOrEmpty(_namespace);

        if (declares)
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

            (outputContext as OutputContext)?.EnterNamespaceScope(_namespace, _innerUsings is { Count: > 0 });

            WriteInnerUsings(outputContext);
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

        if (declares)
        {
            (outputContext as OutputContext)?.ExitNamespaceScope();

            if (!FileScopedNamespace)
            {
                WriteNamespaceClose(outputContext);
            }
        }
    }

    private void WriteInnerUsings(IOutputContext outputContext)
    {
        if (_innerUsings is not { Count: > 0 })
        {
            return;
        }

        foreach (var ns in _innerUsings)
        {
            outputContext.WriteIndentedLine("using " + CSharpIdentifier.EscapeQualified(ns) + ";");
        }

        if (_outputComponents.Count > 0)
        {
            outputContext.WriteLine();
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