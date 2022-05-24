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

        public static TypeDefinition Get(string ns, string name)
        {
            return new TypeDefinition(TypeDefinitionEnum.ClassDefinition, ns, name);
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
                
                var className = genericTypeDefinition.GetGenericName();

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

        private static readonly ITypeDefinition _uintDefinition =
            new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "uint");


        private static readonly ITypeDefinition _shortDefinition =
            new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "short");

        private static readonly ITypeDefinition _ushortDefinition =
            new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "ushort");

        private static readonly ITypeDefinition _longDefinition =
            new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "long");

        private static readonly ITypeDefinition _ulongDefinition =
            new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "ulong");

        private static readonly ITypeDefinition _doubleDefinition =
            new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "double");

        private static readonly ITypeDefinition _decimalDefinition =
            new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "decimal");

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

            if (type == typeof(uint))
            {
                typeDefinition = _uintDefinition;
                return true;
            }

            if (type == typeof(short))
            {
                typeDefinition = _shortDefinition;
                return true;
            }

            if (type == typeof(ushort))
            {
                typeDefinition = _ushortDefinition;
                return true;
            }

            if (type == typeof(long))
            {
                typeDefinition = _longDefinition;
                return true;
            }

            if (type == typeof(ulong))
            {
                typeDefinition = _ulongDefinition;
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
