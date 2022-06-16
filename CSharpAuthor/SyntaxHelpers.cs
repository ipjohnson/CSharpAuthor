using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpAuthor
{
    public static class SyntaxHelpers
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

        public static StaticInvokeStatement Invoke(ITypeDefinition typeDefinition, string methodName, params object[] parameters)
        {
            return new StaticInvokeStatement(typeDefinition, methodName,
                CodeOutputComponent.GetAll(parameters, false).ToList());
        }


        public static StaticInvokeStatement Invoke(Type type, string methodName, params object[] parameters)
        {
            return new StaticInvokeStatement(TypeDefinition.Get(type), methodName,
                CodeOutputComponent.GetAll(parameters, false).ToList());
        }

        public static BaseStatement Base(params object[] parameters)
        {
            var statements = CodeOutputComponent.GetAll(parameters);

            return new BaseStatement(statements.ToList());
        }
    }
}
