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
            return new NewStatement(typeDefinition, parameters);
        }

        public static IOutputComponent NewArray(Type type, int length)
        {
            return NewArray(TypeDefinition.Get(type), length);
        }

        public static IOutputComponent NewArray(ITypeDefinition typeDefinition, int length)
        {
            return new NewArrayStatement(typeDefinition, length);
        }

        public static AwaitStatement Await(IOutputComponent outputComponent)
        {
            return new AwaitStatement(outputComponent);
        }
    }
}
