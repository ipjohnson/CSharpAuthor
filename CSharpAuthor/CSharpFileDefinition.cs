﻿using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class CSharpFileDefinition : BaseOutputComponent, IConstructContainer
{
    private readonly string _namespace;
    private readonly List<IOutputComponent> _outputComponents = new ();

    public CSharpFileDefinition(string ns = "")
    {
        _namespace = ns;
    }

    public ClassDefinition AddClass(string name)
    {
        var classDefinition = new ClassDefinition(name);

        _outputComponents.Add(classDefinition);

        return classDefinition;
    }

    public EnumDefinition AddEnum(string name)
    {
        var enumDefinition = new EnumDefinition(name);

        _outputComponents.Add(enumDefinition);

        return enumDefinition;
    }

    public InterfaceDefinition AddInterface(string interfaceName)
    {
        var interfaceDefinition = new InterfaceDefinition(interfaceName);

        _outputComponents.Add(interfaceDefinition);

        return interfaceDefinition;
    }

    public void AddComponent(IOutputComponent component)
    {
        _outputComponents.Add(component);
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        if (!string.IsNullOrEmpty(_namespace))
        {
            WriteNamespaceOpen(outputContext);
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

        if (!string.IsNullOrEmpty(_namespace))
        {
            WriteNamespaceClose(outputContext);
        }

        outputContext.GenerateUsingStatements();
    }


    private void WriteNamespaceClose(IOutputContext outputContext)
    {
        outputContext.CloseScope();
    }

    private void WriteNamespaceOpen(IOutputContext outputContext)
    {
        outputContext.WriteIndentedLine("namespace " + _namespace);
        outputContext.OpenScope();
    }
}