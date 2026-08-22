using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// One generated <c>.cs</c> file: a namespace and the types declared in it.
/// </summary>
/// <remarks>
/// <para>
/// This is the entry point. Build the tree with the <c>Add*</c> methods, then hand it to an
/// <see cref="OutputContext"/> and read <see cref="OutputContext.Output"/>:
/// </para>
/// <example>
/// <code>
/// var file = new CSharpFileDefinition("Sample");
/// file.AddClass("Greeter");
///
/// var context = new OutputContext();
/// file.WriteOutput(context);
/// var csharp = context.Output();
/// </code>
/// which is
/// <code>
/// namespace Sample
/// {
///     public class Greeter
///     {
///     }
/// }
/// </code>
/// </example>
/// <para>
/// The tree carries no formatting and no output mode of its own, so the same file can be written
/// twice into two differently configured contexts and produce two different - both correct - texts.
/// That is what makes <see cref="OutputContextOptions.TypeOutputMode"/> and the <c>using</c> list a
/// decision made at serialization rather than something every writer has to get right as it goes.
/// </para>
/// </remarks>
public class CSharpFileDefinition : BaseOutputComponent, IConstructContainer
{
    private readonly NamespaceDefinition _namespaceDefinition;

    /// <summary>
    /// Assembly and module attributes, which are written above the namespace rather than in it.
    /// </summary>
    private readonly List<IOutputComponent> _fileLevelComponents = new();

    /// <summary>
    /// A file declaring <paramref name="ns"/>.
    /// </summary>
    /// <remarks>
    /// The default writes no namespace declaration at all and puts the types at the top level of
    /// the file, which compiles and is almost never what a generator wants: a type in the global
    /// namespace collides with every other assembly that did the same thing.
    /// </remarks>
    public CSharpFileDefinition(string ns = "")
    {
        _namespaceDefinition = new NamespaceDefinition(ns);
    }

    /// <summary>
    /// Writes <c>namespace Sample;</c> rather than wrapping the file in <c>namespace Sample { }</c>,
    /// which saves every declaration in the file one level of indent.
    /// </summary>
    /// <remarks>
    /// C# 10. It is not gated on the emit profile - the shape is chosen by the caller, and the
    /// braced form is always legal - so a generator targeting older C# should leave it off.
    /// </remarks>
    public bool FileScopedNamespace
    {
        get => _namespaceDefinition.FileScopedNamespace;
        set => _namespaceDefinition.FileScopedNamespace = value;
    }

    /// <summary>
    /// The top-level declarations in this file that have a name - for a caller inspecting a tree it
    /// did not build, such as a post-processing step looking for a type it needs to extend.
    /// </summary>
    public IEnumerable<IOutputComponent> GetAllNamedComponents() =>
        _namespaceDefinition.GetAllNamedComponents();

    /// <summary>
    /// A class in this file: <c>public class Greeter { }</c>.
    /// </summary>
    /// <remarks>
    /// Returns the class so members can be added to it. <c>public</c> is the default accessibility
    /// here and everywhere else in this library - set <see cref="BaseOutputComponent.Modifiers"/>
    /// for anything else. For a struct or a record, set
    /// <see cref="ClassDefinition.TypeKeyword"/> on what this returns, or call
    /// <see cref="AddRecord"/>.
    /// </remarks>
    public ClassDefinition AddClass(string name)
    {
        return _namespaceDefinition.AddClass(name);
    }

    /// <summary>
    /// A record: the same declaration <see cref="AddClass"/> makes, with
    /// <see cref="ClassDefinition.TypeKeyword"/> already set to <see cref="ClassKeyword.Record"/>.
    /// </summary>
    /// <remarks>
    /// A positional record is a primary constructor plus a semicolon terminator, both on the
    /// <see cref="ClassDefinition"/> this returns:
    /// <example>
    /// <code>
    /// var record = file.AddRecord("Pet");
    /// record.TerminateWithSemicolon = true;
    /// var ctor = record.AddConstructor();
    /// ctor.IsPrimary = true;
    /// ctor.AddParameter(typeof(string), "Id");
    /// </code>
    /// which is <c>public record Pet(string Id);</c>.
    /// </example>
    /// Records require C# 9, and this is one of the features with no downlevel - a profile that
    /// does not have them reports a capability violation rather than writing a class.
    /// </remarks>
    public ClassDefinition AddRecord(string name)
    {
        return _namespaceDefinition.AddRecord(name);
    }

    /// <summary>
    /// An enum: <c>public enum Level { Low, High = 10, }</c>.
    /// </summary>
    /// <remarks>
    /// Add members with <see cref="EnumDefinition.AddValue(string)"/>, or the overload taking a
    /// value where the number matters. Set <see cref="EnumDefinition.BaseType"/> for the underlying
    /// type and call <see cref="EnumDefinition.AddFlags"/> for <c>[Flags]</c>.
    /// </remarks>
    public EnumDefinition AddEnum(string name)
    {
        return _namespaceDefinition.AddEnum(name);
    }

    /// <summary>
    /// An interface: <c>public interface IGreeter { }</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="InterfaceDefinition"/> is a smaller surface than <see cref="ClassDefinition"/> on
    /// purpose - it holds base types, properties and methods, and nothing else. A member that needs
    /// a body is still reachable: an <see cref="InterfaceMethodDefinition"/> with statements in it
    /// is a default interface member.
    /// </remarks>
    public InterfaceDefinition AddInterface(string interfaceName)
    {
        return _namespaceDefinition.AddInterface(interfaceName);
    }

    /// <summary>
    /// Adds a component this class has no <c>Add*</c> method for - a delegate, a nested
    /// <see cref="NamespaceDefinition"/>, a hand-built <see cref="CodeOutputComponent"/>.
    /// </summary>
    /// <remarks>
    /// The escape hatch, and the only way to put a second declaration of the same kind in one file
    /// in a position this class does not name. Components are written in the order they were added,
    /// after nothing and before nothing - unlike the members of a
    /// <see cref="ClassDefinition"/>, which are grouped by kind.
    /// </remarks>
    public void AddComponent(IOutputComponent component)
    {
        // An assembly or module attribute is not a member of the namespace, and C# will not accept
        // it inside one: CS1730 says they must precede every other element in the file bar usings
        // and extern aliases. Added here they were written wherever the namespace body reached, so
        // the only way to emit one correctly was to not use this API.
        if (component is AttributeDefinition { Target: { } target } &&
            (string.Equals(target, "assembly", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(target, "module", StringComparison.OrdinalIgnoreCase)))
        {
            _fileLevelComponents.Add(component);

            return;
        }

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

        // After the header mark, so the generated usings still land above these - which is the one
        // ordering C# accepts for an assembly attribute.
        foreach (var component in _fileLevelComponents)
        {
            component.WriteOutput(outputContext);
        }

        _namespaceDefinition.WriteOutput(outputContext);

        outputContext.GenerateUsingStatements();
    }
}