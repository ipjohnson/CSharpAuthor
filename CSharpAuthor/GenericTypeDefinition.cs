using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpAuthor
{
    public class GenericTypeDefinition : ITypeDefinition
    {
        private readonly int _hashCode;
        private readonly IReadOnlyList<ITypeDefinition> _closingTypes;

        public GenericTypeDefinition(Type type, IReadOnlyList<ITypeDefinition> closeTypes, bool isArray = false, bool isNullable = false) : 
            this(TypeDefinitionEnum.ClassDefinition, type.GetGenericName(), type.Namespace, closeTypes, isArray, isNullable)
        {

        }

        public GenericTypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string name, string ns, IReadOnlyList<ITypeDefinition> closingTypes, bool isArray = false, bool isNullable = false)
        {
            TypeDefinitionEnum = typeDefinitionEnum;
            Name = name;
            Namespace = ns;
            _closingTypes = closingTypes;
            IsArray = isArray;
            IsNullable = isNullable;
        }
        
        public TypeDefinitionEnum TypeDefinitionEnum { get; }

        public bool IsNullable { get; }

        public string Name { get; }

        public string Namespace { get; }

        public bool IsArray { get; }

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

                yield return Namespace;
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

            if (IsArray)
            {
                builder.Append("[]");
            }

            if (IsNullable)
            {
                builder.Append("?");
            }
        }

        public ITypeDefinition MakeNullable()
        {
            return new GenericTypeDefinition(TypeDefinitionEnum, Name, Namespace, _closingTypes, IsArray, true);
        }
    }
}
