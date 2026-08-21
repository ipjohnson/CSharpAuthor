using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// An interface, its base interfaces, and the members it declares.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately a smaller surface than <see cref="ClassDefinition"/>: base types, properties and
/// methods, and nothing else. An interface has no fields, no constructors and no nested types, so
/// there is nothing here to add them with.
/// </para>
/// <example>
/// <code>
/// var greeter = file.AddInterface("IGreeter");
/// greeter.AddBaseType(typeof(IDisposable));
/// greeter.AddProperty(typeof(string), "Name");
/// greeter.AddMethod("Greet").SetReturnType(typeof(string));
/// </code>
/// which is
/// <code>
/// public interface IGreeter : IDisposable
/// {
///     string Greet();
///     string Name { get; set; }
/// }
/// </code>
/// </example>
/// <para>
/// Methods are written before properties, which is the reverse of
/// <see cref="ClassDefinition"/>'s order. Members take no accessibility keyword - the interface
/// decides it - so <see cref="BaseOutputComponent.Modifiers"/> on a member here is ignored;
/// <see cref="ComponentModifier.Partial"/> on the interface itself is not.
/// </para>
/// </remarks>
public class InterfaceDefinition : BaseOutputComponent, INamedComponent
{
    protected readonly List<InterfacePropertyDefinition> Properties = new ();
    protected readonly List<InterfaceMethodDefinition> Methods = new ();
    protected readonly List<ITypeDefinition> BaseTypes = new ();

    /// <summary>
    /// An interface named <paramref name="name"/>. Prefer
    /// <see cref="CSharpFileDefinition.AddInterface"/>, which builds one and attaches it to a file.
    /// </summary>
    /// <remarks>
    /// The leading <c>I</c> is a convention, not a rule, and nothing here adds it - the name is
    /// written as given.
    /// </remarks>
    public InterfaceDefinition(string name)
    {
        Name = name;
    }

    /// <summary>The declared name, escaped with <c>@</c> if it is a keyword.</summary>
    public string Name { get;  }

    /// <inheritdoc cref="AddBaseType(ITypeDefinition)" />
    /// <remarks>
    /// The overload for an interface this generator can name at compile time -
    /// <c>typeof(IDisposable)</c>. <see cref="ClassDefinition.AddBaseType(ITypeDefinition)"/> has
    /// no matching overload, so the two read differently for the same request.
    /// </remarks>
    public InterfaceDefinition AddBaseType(Type type)
    {
        return AddBaseType(TypeDefinition.Get(type));
    }

    /// <summary>
    /// An interface this one extends: <c>public interface IGreeter : IDisposable</c>.
    /// </summary>
    /// <remarks>
    /// Returns the interface, so calls chain. Unlike
    /// <see cref="ClassDefinition.AddBaseType(ITypeDefinition)"/>, nothing is deduplicated here:
    /// the same interface added twice is written twice, which is CS0528 in the generated file.
    /// </remarks>
    public InterfaceDefinition AddBaseType(ITypeDefinition typeDefinition)
    {
        BaseTypes.Add(typeDefinition);

        return this;
    }

    /// <inheritdoc cref="AddProperty(ITypeDefinition, string)" />
    /// <remarks>
    /// The overload for a type this generator can name at compile time. Reach for
    /// <see cref="AddProperty(ITypeDefinition, string)"/> for a type that does not exist as a
    /// <see cref="Type"/> - one this generator is also emitting, one read from a Roslyn symbol, or
    /// one that needed <see cref="ITypeDefinition.MakeNullable"/> applied to it.
    /// </remarks>
    public InterfacePropertyDefinition AddProperty(Type type, string name)
    {
        return AddProperty(TypeDefinition.Get(type), name);
    }

    /// <summary>
    /// A property on the interface: <c>string Name { get; set; }</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both accessors by default. Set <see cref="PropertyDefinition.Set"/> to null on what this
    /// returns for <c>string Name { get; }</c>, which is what an interface usually wants, or set
    /// <see cref="PropertyMethodDefinition.IsInit"/> on it for <c>init</c>.
    /// </para>
    /// <para>
    /// The returned <see cref="InterfacePropertyDefinition"/> is a
    /// <see cref="PropertyDefinition"/> that writes no accessibility keyword, so everything else
    /// about it - including that a property named <c>this</c> with an index is an indexer - reads
    /// the same.
    /// </para>
    /// </remarks>
    public InterfacePropertyDefinition AddProperty(ITypeDefinition typeDefinition, string name)
    {
        var propertyDefinition = new InterfacePropertyDefinition(typeDefinition, name);

        Properties.Add(propertyDefinition);

        return propertyDefinition;
    }

    /// <summary>
    /// A method on the interface: <c>string Greet(string name);</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Declared with a <c>;</c> and no body. Give the returned
    /// <see cref="InterfaceMethodDefinition"/> statements and it becomes a default interface member
    /// with a body instead - which requires C# 8, and is demanded of the emit profile rather than
    /// quietly dropped.
    /// </para>
    /// <para>
    /// <see cref="MethodDefinition.OmitBody"/> is not what makes this bodyless; having no
    /// statements is, and that is the only thing read. Do not set it: on a member that has
    /// statements it suppresses the body without restoring the <c>;</c>, and the declaration comes
    /// out as <c>void M()</c> with nothing after it.
    /// </para>
    /// </remarks>
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

    protected override void WriteComment(IOutputContext outputContext)
    {
        if (string.IsNullOrWhiteSpace(Comment))
        {
            return;
        }
        
        DocumentationComment.WriteSummary(outputContext.WriteIndentedLine, Comment);
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

        outputContext.Write(CSharpIdentifier.Escape(Name));

        if (BaseTypes.Count > 0)
        {
            outputContext.Write(" : ");

            BaseTypes.OutputCommaSeparatedList(outputContext);
        }

        outputContext.WriteLine();
    }
}