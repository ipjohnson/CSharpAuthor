using System;

namespace CSharpAuthor
{
    public class TypeDefinition
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

        public static implicit operator TypeDefinition(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }
            
            if (OpImplicit(type, out var returnTypeDefinition))
            {
                return returnTypeDefinition;
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

            return new TypeDefinition(typeDefinition, type.Namespace ?? "", type.Name);
        }
        
        private static bool OpImplicit(Type type, out TypeDefinition returnTypeDefinition)
        {
            if (typeof(string) == type)
            {
                returnTypeDefinition = new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "string");
                return true;
            }

            returnTypeDefinition = null;

            return false;
        }
    }
}
