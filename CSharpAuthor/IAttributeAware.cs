using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public interface IAttributeAware
    {
        AttributeDefinition AddAttribute(Type type, string argumentStatement = "");
        
        AttributeDefinition AddAttribute(ITypeDefinition typeDefinition, string argumentStatement = "");
    }
}
