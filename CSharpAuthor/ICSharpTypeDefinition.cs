using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;

namespace CSharpAuthor
{
    public interface ITypeDefinition
    {
        TypeDefinitionEnum TypeDefinitionEnum { get; }

        string Name { get; }

        string Namespace { get; }

        IEnumerable<string> KnownNamespaces { get; }

        void WriteShortName(StringBuilder builder);
    }
}
