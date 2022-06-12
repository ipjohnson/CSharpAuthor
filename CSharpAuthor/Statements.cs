using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public static class Statements
    {
        public static IOutputComponent New(Type type, params object[] parameters)
        {
            return New(TypeDefinition.Get(type), parameters);
        }

        public static IOutputComponent New(ITypeDefinition typeDefinition, params object[] parameters)
        {
            return null;
        }

    }
}
