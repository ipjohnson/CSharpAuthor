using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class TypeDefinition : ITypeDefinition 
    {
        private readonly int _hashCode;

        public TypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string ns, string name)
        {
            TypeDefinitionEnum = typeDefinitionEnum;
            Namespace = ns;
            Name = name;

            _hashCode = $"{TypeDefinitionEnum}:{Namespace}:{Name}".GetHashCode();
        }

        public TypeDefinitionEnum TypeDefinitionEnum { get; }

        public string Namespace { get; }

        public string Name { get; }

        public IEnumerable<string> KnownNamespaces
        {
            get { yield return Namespace; }
        }

        public void WriteShortName(StringBuilder builder)
        {
            builder.Append(Name);
        }

        public override bool Equals(object obj)
        {
            if (obj is TypeDefinition typeDefinition)
            {
                return typeDefinition.TypeDefinitionEnum == TypeDefinitionEnum &&
                    typeDefinition.Name == Name && 
                    typeDefinition.Namespace == Namespace;
            }

            return false; 
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        public override string ToString()
        {
            return $"{TypeDefinitionEnum}:{Namespace}:{Name}";
        }

        public static ITypeDefinition Get(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (IsKnownType(type, out var knownDefinition))
            {
                return knownDefinition;
            }

            var typeDefinition = CSharpAuthor.TypeDefinitionEnum.ClassDefinition;

            if (type.IsEnum)
            {
                typeDefinition = TypeDefinitionEnum.EnumDefinition;
            }
            else if (type.IsInterface)
            {
                typeDefinition = TypeDefinitionEnum.InterfaceDefinition;
            }
            
            if (type.IsConstructedGenericType)
            {
                var genericTypeDefinition = type.GetGenericTypeDefinition();

                var tickIndex = genericTypeDefinition.Name.IndexOf('`');
                var className = genericTypeDefinition.Name.Substring(0, tickIndex);

                var closingTypes = new List<ITypeDefinition>();

                foreach (var genericArgument in type.GetGenericArguments())
                {
                    closingTypes.Add(Get(genericArgument));
                }

                return new GenericTypeDefinition(typeDefinition, className,
                    genericTypeDefinition.Namespace, closingTypes);
            }

            return new TypeDefinition(typeDefinition, type.Namespace, type.Name);
        }

        private static readonly ITypeDefinition _stringDefinition =
            new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "string");

        private static readonly ITypeDefinition _intDefinition =
            new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "int");

        private static readonly ITypeDefinition _doubleDefinition =
            new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "double");
        
        private static readonly ITypeDefinition _boolDefinition =
            new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "bool");
        
        private static bool IsKnownType(Type type, out ITypeDefinition typeDefinition)
        {
            if (type == typeof(string))
            {
                typeDefinition = _stringDefinition;
                return true;
            }
            
            if (type == typeof(int))
            {
                typeDefinition = _intDefinition;
                return true;
            }

            if (type == typeof(double))
            {
                typeDefinition = _doubleDefinition;
                return true;
            }

            if (type == typeof(bool))
            {
                typeDefinition = _boolDefinition;
                return true;
            }

            typeDefinition = null;
            return false;
        }
    }
}
