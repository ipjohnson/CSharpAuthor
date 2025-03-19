using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;

namespace CSharpAuthor;

public interface ITypeDefinition : IComparable<ITypeDefinition>
{
    TypeDefinitionEnum TypeDefinitionEnum { get; }

    bool IsNullable { get; }

    bool IsArray { get; }

    string Name { get; }

    string Namespace { get; }

    IEnumerable<string> KnownNamespaces { get; }

    void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName);

    ITypeDefinition MakeNullable(bool nullable = true);

    ITypeDefinition MakeArray();

    IReadOnlyList<ITypeDefinition> TypeArguments { get; }
}

public static class ITypeDefinitionExtensions
{
    public static string GetShortName(this ITypeDefinition typeDefinition)
    {
        var stringBuilder = new StringBuilder();

        typeDefinition.WriteTypeName(stringBuilder);

        return stringBuilder.ToString();
    }
}