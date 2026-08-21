using System;
using System.Collections.Generic;

namespace CSharpAuthor;

public abstract class BaseOutputComponent : IOutputComponent
{
    protected List<AttributeDefinition>? AttributeDefinitions;
    protected List<string>? UsingNamespaces;
    protected List<IOutputComponent>? LeadingTraits;
    protected List<IOutputComponent>? TrailingTraits;

    public ComponentModifier Modifiers { get; set; } = ComponentModifier.None;

    public string? Comment { get; set; }

    public bool Indented { get; set; } = true;

    public void AddLeadingTrait(IOutputComponent outputComponent)
    {
        LeadingTraits ??= new List<IOutputComponent>();

        LeadingTraits.Add(outputComponent);
    }

    public void AddTrailingTrait(IOutputComponent component)
    {
        TrailingTraits ??= new List<IOutputComponent>();
        
        TrailingTraits.Add(component);
    }
    
    public AttributeDefinition AddAttribute(Type type, params object[] args)
    {
        return AddAttribute(TypeDefinition.Get(type), args);
    }

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

    public void AddUsingNamespace(string ns)
    {
        if (UsingNamespaces == null)
        {
            UsingNamespaces = new List<string>();
        }

        UsingNamespaces.Add(ns);
    }

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