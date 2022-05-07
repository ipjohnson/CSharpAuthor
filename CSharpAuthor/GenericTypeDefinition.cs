using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpAuthor
{
    public class GenericTypeDefinition : ITypeDefinition
    {
        private readonly int _hashCode;
        private readonly string ns;
        private readonly IReadOnlyList<ITypeDefinition> _closingTypes;

        public GenericTypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string name, string ns, IReadOnlyList<ITypeDefinition> closingTypes)
        {
            TypeDefinitionEnum = typeDefinitionEnum;
            Name = name;
            Namespace = ns;
            _closingTypes = closingTypes;
        }
        
        public TypeDefinitionEnum TypeDefinitionEnum { get; }

        public string Name { get; }

        public string Namespace { get; }

        public IEnumerable<string> KnownNamespaces
        {
            get
            {
                foreach (var typeDefinition in _closingTypes)
                {
                    foreach (var knownNamespace in typeDefinition.KnownNamespaces)
                    {
                        yield return knownNamespace;
                    }
                }

                yield return ns;
            }
        }

        public void WriteShortName(StringBuilder builder)
        {
            builder.Append(Name);
            builder.Append('<');

            var writeComma = false;

            foreach (var typeDefinition in _closingTypes)
            {
                if (writeComma)
                {
                    builder.Append(',');
                }
                else
                {
                    writeComma = true;
                }
                typeDefinition.WriteShortName(builder);
            }

            builder.Append('>');
        }
    }
}
