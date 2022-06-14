using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class TypeDefinition : ITypeDefinition 
    {
        private readonly int _hashCode;

        public TypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string ns, string name, bool isArray)
        {
            TypeDefinitionEnum = typeDefinitionEnum;
            Namespace = ns;
            Name = name;
            IsArray = isArray;

            _hashCode = $"{TypeDefinitionEnum}:{Namespace}:{Name}".GetHashCode();
        }

        public TypeDefinitionEnum TypeDefinitionEnum { get; }

        public string Namespace { get; }

        public string Name { get; }

        public bool IsArray { get; }

        public IEnumerable<string> KnownNamespaces
        {
            get { yield return Namespace; }
        }

        public void WriteShortName(StringBuilder builder)
        {
            builder.Append(Name);

            if (IsArray)
            {
                builder.Append("[]");
            }
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

        public static TypeDefinition Get(string ns, string name, bool isArray = false)
        {
            return new TypeDefinition(TypeDefinitionEnum.ClassDefinition, ns, name, isArray);
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
                    genericTypeDefinition.Namespace, closingTypes, type.IsArray);
            }

            return new TypeDefinition(typeDefinition, type.Namespace, type.Name, type.IsArray);
        }

        private static readonly Dictionary<Type, ITypeDefinition> _knownTypes = new Dictionary<Type, ITypeDefinition>
        {
            {typeof(object), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "object", false)},
            {typeof(ulong), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "ulong", false)},
            {typeof(long), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "long", false)},
            {typeof(uint), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "uint", false)},
            {typeof(string), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "string", false)},
            {typeof(int), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "int", false)},
            {typeof(short), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "short", false)},
            {typeof(ushort), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "ushort", false)},
            {typeof(byte), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "byte", false)},
            {typeof(double), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "double", false)},
            {typeof(decimal), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "decimal", false)},
            {typeof(bool), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "bool", false)},
        };
        private static readonly Dictionary<Type,ITypeDefinition> _knownArrayTypes = new Dictionary<Type, ITypeDefinition>
        {
            {typeof(object[]), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "object", true)},
            {typeof(string[]), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "string", true)},
            {typeof(int[]), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "int", true)},
            {typeof(uint[]), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "uint", true)},
            {typeof(long[]), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "long", true)},
            {typeof(ulong[]), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "ulong", true)},
            {typeof(short[]), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "short", true)},
            {typeof(ushort[]), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "ushort", true)},
            {typeof(byte[]), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "byte", true)},
            {typeof(bool[]), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "bool", true)},
            {typeof(double[]), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "double", true)},
            {typeof(decimal[]), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "decimal", true)},
        };

        private static bool IsKnownType(Type type, out ITypeDefinition? typeDefinition)
        {
            if (type.IsArray)
            {
                return _knownArrayTypes.TryGetValue(type, out typeDefinition);
            }

            return _knownTypes.TryGetValue(type, out typeDefinition);
        }
    }
}
