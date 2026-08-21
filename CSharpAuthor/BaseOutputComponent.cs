using System;
using System.Collections.Generic;

namespace CSharpAuthor;

/// <summary>
/// What every declaration in this library has in common: an accessibility, a documentation
/// comment, attributes, and the traits written around it.
/// </summary>
public abstract class BaseOutputComponent : IOutputComponent
{
    protected List<AttributeDefinition>? AttributeDefinitions;
    protected List<string>? UsingNamespaces;
    protected List<IOutputComponent>? LeadingTraits;
    protected List<IOutputComponent>? TrailingTraits;

    /// <summary>
    /// The keywords in front of the declaration: accessibility, and the modifiers that go with it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Left at <see cref="ComponentModifier.None"/>, a declaration writes <c>public</c> - the
    /// default everywhere in this library except <see cref="FieldDefinition"/>, which writes
    /// <c>private</c>. <see cref="ComponentModifier.NoAccessibility"/> is how a caller asks for
    /// no keyword at all, which is not the same request as <c>None</c>.
    /// </para>
    /// <para>
    /// It is a <see cref="FlagsAttribute"/> enum, so the two-keyword accessibility levels are
    /// combinations: <c>private protected</c> is
    /// <see cref="ComponentModifier.PrivateProtected"/>, and constructing it by hand as
    /// <c>Private | Protected</c> is the same value. Only the modifiers that make sense for the
    /// declaration are written, so setting <c>Async</c> on a class is ignored rather than an error.
    /// </para>
    /// </remarks>
    public ComponentModifier Modifiers { get; set; } = ComponentModifier.None;

    /// <summary>
    /// The documentation comment written above the declaration, as the body of a
    /// <c>&lt;summary&gt;</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Plain text, not XML: the <c>&lt;summary&gt;</c> tags and the <c>///</c> prefixes are added
    /// here. A newline in the text becomes a new <c>///</c> line, so a two-sentence comment is
    /// written as two lines rather than one long one.
    /// </para>
    /// <example>
    /// <c>definition.Comment = "A greeter.";</c> is
    /// <code>
    /// /// &lt;summary&gt;
    /// /// A greeter.
    /// /// &lt;/summary&gt;
    /// </code>
    /// </example>
    /// <para>
    /// Parameters and return values are documented where they are declared -
    /// <see cref="ParameterDefinition"/> and <see cref="MethodDefinition.ReturnComment"/> - and are
    /// only written when the member itself has a comment, because <c>&lt;param&gt;</c> with no
    /// <c>&lt;summary&gt;</c> is a documentation comment with nothing in it. Nothing is written at
    /// all when <see cref="OutputContextOptions.GenerateDocumentation"/> is off.
    /// </para>
    /// </remarks>
    public string? Comment { get; set; }

    /// <summary>
    /// Whether the component starts on a line of its own, at the current indent.
    /// </summary>
    /// <remarks>
    /// True for a declaration and for a statement. It is set to false by anything that puts a
    /// component <em>inside</em> a line - a <c>for</c> header, an assignment's right-hand side, an
    /// argument - so the same component type can be a statement in one position and an expression
    /// in another without a second class existing for it.
    /// </remarks>
    public bool Indented { get; set; } = true;

    /// <summary>
    /// A component written immediately before this declaration, after its comment and attributes -
    /// a <c>#region</c>, a <c>#pragma warning disable</c>, a <c>#nullable enable</c>.
    /// </summary>
    /// <remarks>
    /// For the things that are not part of the declaration but have to sit against it. See
    /// <see cref="SyntaxHelpers.WrapInPragma"/> and <see cref="SyntaxHelpers.EnableNullable"/>,
    /// which are this and <see cref="AddTrailingTrait"/> used in pairs.
    /// </remarks>
    public void AddLeadingTrait(IOutputComponent outputComponent)
    {
        LeadingTraits ??= new List<IOutputComponent>();

        LeadingTraits.Add(outputComponent);
    }

    /// <summary>
    /// A component written immediately after this declaration - the closing half of whatever
    /// <see cref="AddLeadingTrait"/> opened.
    /// </summary>
    public void AddTrailingTrait(IOutputComponent component)
    {
        TrailingTraits ??= new List<IOutputComponent>();

        TrailingTraits.Add(component);
    }

    /// <inheritdoc cref="AddAttribute(ITypeDefinition, object[])" />
    /// <remarks>
    /// The overload to reach for when the attribute is a type this generator can name -
    /// <c>typeof(ObsoleteAttribute)</c>. Use
    /// <see cref="AddAttribute(ITypeDefinition, object[])"/> for an attribute that does not exist
    /// yet, which is the usual case for one this generator is also emitting.
    /// </remarks>
    public AttributeDefinition AddAttribute(Type type, params object[] args)
    {
        return AddAttribute(TypeDefinition.Get(type), args);
    }

    /// <summary>
    /// An attribute on this declaration: <c>[Obsolete("use Greeter2")]</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>Attribute</c> postfix is taken off when it is written, so
    /// <c>typeof(ObsoleteAttribute)</c> is written <c>[Obsolete]</c> - see
    /// <see cref="AttributeTypeReference"/>. The namespace is derived from the type like any other
    /// type reference, so nothing has to ask for a <c>using</c> and nothing is left qualified in a
    /// mode that does not qualify.
    /// </para>
    /// <para>
    /// <strong>Arguments are expressions, not values.</strong> Each one that is not already an
    /// <see cref="IOutputComponent"/> is written as its text, so a <see cref="string"/> arrives
    /// unquoted: <c>AddAttribute(typeof(ObsoleteAttribute), "use Greeter2")</c> emits
    /// <c>[Obsolete(use Greeter2)]</c>, which does not compile. Pass
    /// <see cref="SyntaxHelpers.QuoteString"/> for a string literal and
    /// <see cref="SyntaxHelpers.TypeOf"/> for a <c>typeof</c>:
    /// </para>
    /// <example>
    /// <code>
    /// definition.AddAttribute(typeof(ObsoleteAttribute), SyntaxHelpers.QuoteString("use Greeter2"));
    /// // [Obsolete("use Greeter2")]
    ///
    /// definition.AddAttribute(converterAttribute, SyntaxHelpers.TypeOf(myConverter));
    /// // [Converter(typeof(MyConverter))]
    /// </code>
    /// </example>
    /// <para>
    /// Returns the attribute, which is where <see cref="AttributeDefinition.Target"/> lives - the
    /// <c>[property: Key]</c> form a positional record needs.
    /// </para>
    /// </remarks>
    public AttributeDefinition AddAttribute(ITypeDefinition typeDefinition, params object[] args)
    {
        if (AttributeDefinitions == null)
        {
            AttributeDefinitions = new List<AttributeDefinition>();
        }

        var arguments = new List<IOutputComponent>();

        foreach (var arg in args)
        {
            if (arg is IOutputComponent outputComponent)
            {
                arguments.Add(outputComponent);
            }
            else
            {
                arguments.Add(CodeOutputComponent.Get(arg));
            }
        }
        
        var attribute = new AttributeDefinition(typeDefinition){ Arguments = arguments };

        AttributeDefinitions.Add(attribute);

        return attribute;
    }

    /// <summary>
    /// Asks the file for <c>using <paramref name="ns"/>;</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>You almost never need this.</strong> A namespace that exists because of a
    /// <em>type</em> is derived from the types the file actually wrote, so writing a
    /// <see cref="ITypeDefinition"/> anywhere brings its namespace with it and a missing
    /// <c>using</c> is not a failure that can happen. Asking for one by hand alongside a type is
    /// how a file that qualifies every name still ended up carrying a directive it did not need.
    /// </para>
    /// <para>
    /// It is for the one thing qualification cannot express: an extension method is found through a
    /// <c>using</c> and no other way, so a file calling <c>source.Count()</c> needs
    /// <c>using System.Linq;</c> even in <see cref="TypeOutputMode.Global"/>. That is why these are
    /// still emitted in a qualifying mode, and why
    /// <see cref="OutputContextOptions.EmitExplicitUsings"/> exists to turn even that off.
    /// </para>
    /// <para>
    /// Duplicates are harmless - the directives are deduplicated and sorted when the file is
    /// serialized - and a request for the file's own namespace is dropped.
    /// </para>
    /// </remarks>
    public void AddUsingNamespace(string ns)
    {
        if (UsingNamespaces == null)
        {
            UsingNamespaces = new List<string>();
        }

        UsingNamespaces.Add(ns);
    }

    /// <summary>
    /// Records this component into <paramref name="outputContext"/>: comment, leading traits,
    /// attributes, the declaration itself, then trailing traits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing here produces text. The context records what was written as segments and turns them
    /// into C# in <see cref="OutputContext.Output"/>, which is why the same tree written into two
    /// contexts gives two different files:
    /// </para>
    /// <example>
    /// <code>
    /// var file = new CSharpFileDefinition("Sample");
    /// file.AddClass("Greeter").AddProperty(TypeDefinition.Get("Sample.Models", "Result"), "A");
    ///
    /// var shortName = new OutputContext();
    /// file.WriteOutput(shortName);
    /// // using Sample.Models;  ...  public Result A { get; set; }
    ///
    /// var global = new OutputContext(
    ///     new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });
    /// file.WriteOutput(global);
    /// // no usings        ...  public global::Sample.Models.Result A { get; set; }
    /// </code>
    /// </example>
    /// <para>
    /// A context is written into once. Writing two files into one context produces one text with
    /// both in it, which is a legal C# file and is almost never what was meant - make a context per
    /// file. Reading <see cref="OutputContext.Output"/> more than once is fine and gives the same
    /// answer each time.
    /// </para>
    /// </remarks>
    public void WriteOutput(IOutputContext outputContext)
    {
        if (UsingNamespaces != null)
        {
            outputContext.AddImportNamespaces(UsingNamespaces);
        }

        if (outputContext.Options.GenerateDocumentation)
        {
            WriteComment(outputContext);
        }

        ProcessLeadingTraits(outputContext);
        
        ProcessAttributes(outputContext);

        WriteComponentOutput(outputContext);

        ProcessTrailingTraits(outputContext);
    }

    protected virtual void WriteComment(IOutputContext outputContext)
    {
        
    }

    protected virtual void ProcessTrailingTraits(IOutputContext outputContext)
    {
        if (TrailingTraits == null) return;

        foreach (var trailingTrait in TrailingTraits)
        {
            trailingTrait.WriteOutput(outputContext);
        }
    }

    protected virtual void ProcessLeadingTraits(IOutputContext outputContext)
    {
        if (LeadingTraits == null) return;

        foreach (var leadingTrait in LeadingTraits)
        {
            leadingTrait.WriteOutput(outputContext);
        }
    }

    protected virtual void ProcessAttributes(IOutputContext outputContext)
    {
        if (AttributeDefinitions == null) return;

        foreach (var attributeDefinition in AttributeDefinitions)
        {
            attributeDefinition.WriteComponentOutput(outputContext);
        }
    }

    protected abstract void WriteComponentOutput(IOutputContext outputContext);

    protected string GetVirtualModifier()
    {
        if ((Modifiers & ComponentModifier.Virtual) == ComponentModifier.Virtual)
        {
            return KeyWords.Virtual;
        }

        if ((Modifiers & ComponentModifier.Override) == ComponentModifier.Override)
        {
            return KeyWords.Override;
        }

        return "";
    }

    /// <summary>
    /// The accessibility keywords for <see cref="Modifiers"/>, or <paramref name="defaultString"/>
    /// when none was asked for.
    /// </summary>
    /// <remarks>
    /// The two-keyword levels are tested first, and they have to be: <see cref="ComponentModifier"/>
    /// is a flags enum, so <c>private protected</c> is <c>Private | Protected</c> and matches both
    /// single-flag tests below. Reading one flag at a time returned <c>protected</c> for it, which
    /// is a wider accessibility than the caller declared - and <c>internal</c> for
    /// <c>protected internal</c>, which is narrower in one direction and wider in the other. Both
    /// compiled.
    /// </remarks>
    protected string GetAccessModifier(string defaultString)
    {
        return Modifiers.GetAccessibilityKeywords(defaultString);
    }
}