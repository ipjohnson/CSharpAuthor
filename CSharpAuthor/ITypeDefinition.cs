using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;

namespace CSharpAuthor
{
    public interface ITypeDefinition : IComparable<ITypeDefinition>
    {
        TypeDefinitionEnum TypeDefinitionEnum { get; }

        bool IsNullable { get; }

        bool IsArray { get; }

        string Name { get; }

        string Namespace { get; }

        IEnumerable<string> KnownNamespaces { get; }

        void WriteShortName(StringBuilder builder);

        ITypeDefinition MakeNullable();

        ITypeDefinition MakeArray();
    }

    public static class ITypeDefinitionExtensions
    {
        public static string GetShortName(this ITypeDefinition typeDefinition)
        {
            var stringBuilder = new StringBuilder();

            typeDefinition.WriteShortName(stringBuilder);

            return stringBuilder.ToString();
        }
    }
}
