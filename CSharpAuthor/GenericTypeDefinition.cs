using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpAuthor
{
    public class GenericTypeDefinition : BaseTypeDefinition
    {
        private readonly int _hashCode;
        private readonly IReadOnlyList<ITypeDefinition> _closingTypes;

        public GenericTypeDefinition(Type type, IReadOnlyList<ITypeDefinition> closeTypes, bool isArray = false,
            bool isNullable = false) :
            this(TypeDefinitionEnum.ClassDefinition, type.Namespace, type.GetGenericName(),  closeTypes, isArray, isNullable)
        {

        }

        public GenericTypeDefinition(TypeDefinitionEnum classType, string ns, string name, IReadOnlyList<ITypeDefinition> closingTypes,
            bool isArray = false, bool isNullable = false) : base(classType, ns, name, isArray, isNullable)
        {
            _closingTypes = closingTypes;
        }
        
        public override int CompareTo(ITypeDefinition other)
        {
            var baseCompare = BaseCompareTo(other);

            if (baseCompare != 0)
            {
                return baseCompare;
            }

            if (other is not GenericTypeDefinition genericTypeDefinition)
            {
                return -1;
            }

            if (genericTypeDefinition._closingTypes.Count != _closingTypes.Count)
            {
                return genericTypeDefinition._closingTypes.Count - _closingTypes.Count;
            }

            for (var i = 0; i < _closingTypes.Count; i++)
            {
                var compareValue = _closingTypes[i].CompareTo(genericTypeDefinition._closingTypes[i]);

                if (compareValue != 0)
                {
                    return compareValue;
                }
            }

            return 0;
        }

        public override IEnumerable<string> KnownNamespaces
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

        public override void WriteShortName(StringBuilder builder)
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

        public override ITypeDefinition MakeNullable()
        {
            return new GenericTypeDefinition(TypeDefinitionEnum, Namespace, Name, _closingTypes, IsArray, true);
        }
    }
}
