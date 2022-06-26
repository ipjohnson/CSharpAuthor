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
                CodeOutputComponent.GetAll(parameters, false).ToList()) { Indented = false };
        }


        public static StaticInvokeGenericStatement InvokeGeneric(Type type, string methodName, IReadOnlyList<ITypeDefinition> genericArgs, params object[] parameters)
        {
            return new StaticInvokeGenericStatement(TypeDefinition.Get(type), methodName, genericArgs,
                CodeOutputComponent.GetAll(parameters, false).ToList()) { Indented = false };
        }

        public static StaticInvokeGenericStatement InvokeGeneric(ITypeDefinition typeDefinition, string methodName, IReadOnlyList<ITypeDefinition> genericArgs, params object[] parameters)
        {
            return new StaticInvokeGenericStatement(
                    typeDefinition, 
                    methodName, 
                    genericArgs,
                    CodeOutputComponent.GetAll(parameters, false).ToList()) { Indented = false };
        }
        
        public static StaticInvokeStatement Invoke(Type type, string methodName, params object[] parameters)
        {
            return new StaticInvokeStatement(TypeDefinition.Get(type), methodName,
                    CodeOutputComponent.GetAll(parameters, false).ToList())
                { Indented = false };
        }

        public static InvokeGenericDefinition InvokeGeneric(string methodName, IReadOnlyList<ITypeDefinition> genericArgs, params object[] parameters)
        {
            return new InvokeGenericDefinition("", methodName, genericArgs, parameters)
                { Indented = false };
        }
        
        public static InvokeDefinition Invoke(string methodName, params object[] parameters)
        {
            return new InvokeDefinition("", methodName, parameters)
                { Indented = false };
        }

        public static BaseStatement Base(params object[] parameters)
        {
            var statements = CodeOutputComponent.GetAll(parameters);

            return new BaseStatement(statements.ToList());
        }

        public static IOutputComponent This(params object[] parameters)
        {
            var statements = CodeOutputComponent.GetAll(parameters);

            return new WrapStatement(new ListOutputComponent(statements.ToList()), "this(", ")");
        }

        public static string QuoteString(string stringValue)
        {
            return "\"" + stringValue + "\"";
        }

        public static IOutputComponent Null()
        {
            return CodeOutputComponent.Get("null");
        }

        public static IOutputComponent TypeOf(ITypeDefinition typeDefinition)
        {
            return new WrapStatement(new TypeStatement(typeDefinition), "typeof(", ")");
        }

        public static LogicStatement And(params object[] andStatements)
        {
            return new LogicStatement(" && ", andStatements);
        }


        public static LogicStatement And(IReadOnlyList<IOutputComponent> andStatements)
        {
            return new LogicStatement(" && ", andStatements);
        }

        public static LogicStatement Or(params object[] orStatements)
        {
            return new LogicStatement(" || ", orStatements);
        }

        public static LogicStatement Or(IReadOnlyList<IOutputComponent> orStatements)
        {
            return new LogicStatement(" || ", orStatements);
        }

        public static LogicStatement EqualsStatement(object leftHandSide, object rightHandSide)
        {
            return new LogicStatement(" == ", leftHandSide, rightHandSide);
        }

        public static LogicStatement NotEquals(object leftHandSide, object rightHandSide)
        {
            return new LogicStatement(" != ", leftHandSide, rightHandSide);
        }

        public static LogicStatement GreaterThan(object leftHandSide, object rightHandSide)
        {
            return new LogicStatement(" > ", leftHandSide, rightHandSide);
        }
        public static LogicStatement LessThan(object leftHandSide, object rightHandSide)
        {
            return new LogicStatement(" < ", leftHandSide, rightHandSide);
        }

        public static LogicStatement GreaterThanOrEquals(object leftHandSide, object rightHandSide)
        {
            return new LogicStatement(" >= ", leftHandSide, rightHandSide);
        }
        public static LogicStatement LessThanOrEquals(object leftHandSide, object rightHandSide)
        {
            return new LogicStatement(" <= ", leftHandSide, rightHandSide);
        }
    }
}
