using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpAuthor
{
    public class TypeDefinition
    {
        public TypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string ns, string name)
        {
            TypeDefinitionEnum = typeDefinitionEnum;
            Namespace = ns;
            Name = name;
        }

        public TypeDefinitionEnum TypeDefinitionEnum { get; }

        public string Namespace { get; }

        public string Name { get; }
        

        public static implicit operator TypeDefinition(Type type)
        {
            if (typeof(string) == type)
            {
                return new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "string");
            }

            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
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
    }
}
