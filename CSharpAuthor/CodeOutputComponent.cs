using System;
using System.Collections;
using System.Collections.Generic;

namespace CSharpAuthor;

public class CodeOutputComponent : BaseOutputComponent
{
    private readonly string _statement;
    private List<ITypeDefinition>? _typeDefinitions;

    public CodeOutputComponent(string statement)
    {
        _statement = statement;
    }

    public static IEnumerable<IOutputComponent> GetAll(IEnumerable<object> values, bool indented = false)
    {
        foreach (var objectValue in values)
        {
            yield return Get(objectValue, indented);
        }
    }

    public static IOutputComponent Get(object? value, bool indented = false)
    {
        return value switch
        {
            null => new CodeOutputComponent("") { Indented = indented },

            IOutputComponent outputComponent => outputComponent,
            
            _ => DefaultComponent(value, indented)
        };
    }

    private static IOutputComponent DefaultComponent(object value, bool indented) {
        if (value is IEnumerable<string> stringValues)
        {
            return GetNewStringArray(stringValues, indented);
        }
        
        if (value is Array values)
        {
            return GetNewArray(values, indented);
        }

        if (value is bool booleanValue)
        {
            return new CodeOutputComponent(booleanValue ? "true" : "false") { Indented = indented };
        }
        
        return new CodeOutputComponent(value.ToString()) { Indented = indented };
    }

    private static IOutputComponent GetNewStringArray(IEnumerable<string> stringValues, bool indented)
    {
        var values = new List<IOutputComponent>();

        foreach (var stringValue in stringValues)
        {
            values.Add(Get(SyntaxHelpers.QuoteString(stringValue)));
        }
        
        return new NewArrayStatement(TypeDefinition.Get(typeof(string)), values.ToArray());
    }

    private static IOutputComponent GetNewArray(IEnumerable values, bool indented)
    {
        var outputComponents = new List<IOutputComponent>();
        
        Type? type = null;

        if (values is Array array)
        {
            type = array.GetType().GetElementType();
        }
        
        foreach (var value in values)
        {
            if (type == null)
            {
                type = value.GetType();
            }
            
            outputComponents.Add(Get(value));
        }
        
        return new NewArrayStatement(
            TypeDefinition.Get(type ?? typeof(object)), outputComponents.ToArray());
    }

    public void AddType(ITypeDefinition typeDefinition)
    {
        _typeDefinitions ??= new List<ITypeDefinition>();

        _typeDefinitions.Add(typeDefinition);
    }

    public void AddTypes(IEnumerable<ITypeDefinition> typeDefinitions)
    {
        _typeDefinitions ??= new List<ITypeDefinition>();

        _typeDefinitions.AddRange(typeDefinitions);
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        if (Indented)
        {
            outputContext.WriteIndentedLine(_statement);
        }
        else
        {
            outputContext.Write(_statement);
        }

        if (_typeDefinitions != null)
        {
            outputContext.AddImportNamespaces(_typeDefinitions);
        }

    }
    public static implicit operator CodeOutputComponent(string statement) => new(statement);
}