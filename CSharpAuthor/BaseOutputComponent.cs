﻿using System;
using System.Collections.Generic;

namespace CSharpAuthor;

public abstract class BaseOutputComponent : IOutputComponent
{
    protected List<AttributeDefinition>? AttributeDefinitions;
    protected List<string>? UsingNamespaces;

    public ComponentModifier Modifiers { get; set; } = ComponentModifier.None;

    public string? Comment { get; set; }

    public bool Indented { get; set; } = true;

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

        ProcessAttributes(outputContext);

        WriteComponentOutput(outputContext);
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

    protected string GetAccessModifier(string defaultString)
    {
        if ((Modifiers & ComponentModifier.Public) == ComponentModifier.Public)
        {
            return KeyWords.Public;
        }

        if ((Modifiers & ComponentModifier.Protected) == ComponentModifier.Protected)
        {
            return KeyWords.Protected;
        }

        if ((Modifiers & ComponentModifier.Private) == ComponentModifier.Private)
        {
            return KeyWords.Private;
        }

        return defaultString;
    }
        
}