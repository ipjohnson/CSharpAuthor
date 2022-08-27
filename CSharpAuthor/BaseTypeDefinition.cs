using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public abstract class BaseTypeDefinition : ITypeDefinition
    {
        protected BaseTypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string ns, string name, bool isArray, bool isNullable)
        {
            Name = name;
            Namespace = ns;
            IsNullable = isNullable;
            IsArray = isArray;
            TypeDefinitionEnum = typeDefinitionEnum;
        }

        public string Name { get; }

        public string Namespace { get; }

        public abstract IEnumerable<string> KnownNamespaces { get; }

        public abstract void WriteShortName(StringBuilder builder);

        public abstract ITypeDefinition MakeNullable();

        public abstract ITypeDefinition MakeArray();

        public TypeDefinitionEnum TypeDefinitionEnum { get; }

        public bool IsNullable { get; }
        
        public bool IsArray { get; }

        public abstract int CompareTo(ITypeDefinition other);

        protected int BaseCompareTo(ITypeDefinition other)
        {
            if (ReferenceEquals(null, other))
            {
                return 1;
            }

            if (TypeDefinitionEnum != other.TypeDefinitionEnum)
            {
                return TypeDefinitionEnum - other.TypeDefinitionEnum;
            }

            var nameCompare = string.Compare(Name, other.Name, StringComparison.Ordinal);

            if (nameCompare != 0)
            {
                return nameCompare;
            }

            var namespaceCompare = string.Compare(Namespace, other.Namespace, StringComparison.Ordinal);

            if (namespaceCompare != 0)
            {
                return namespaceCompare;
            }

            if (IsArray != other.IsArray)
            {
                return IsArray ? 1 : -1;
            }

            if (IsNullable != other.IsNullable)
            {
                return IsNullable ? 1 : -1;
            }

            return 0;
        }
    }
}
