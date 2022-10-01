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

            _ => new CodeOutputComponent(value.ToString()) { Indented = indented }
        };
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