﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpAuthor;

public static class SyntaxHelpers
{
    public static NewStatement New(Type type, params object[] parameters)
    {
        return New(TypeDefinition.Get(type), parameters);
    }

    public static NewStatement New(ITypeDefinition typeDefinition, params object[] parameters)
    {
        return new NewStatement(typeDefinition, parameters);
    }

    public static NewArrayStatement NewArray(Type type, int length)
    {
        return NewArray(TypeDefinition.Get(type), length);
    }

    public static NewArrayStatement NewArray(ITypeDefinition typeDefinition, int length)
    {
        return new NewArrayStatement(typeDefinition, length);
    }
    
    public static NewArrayStatement NewArray(Type type, params object[] parameters)
    {
        return new NewArrayStatement(TypeDefinition.Get(type), parameters.Select(p => CodeOutputComponent.Get(p)).ToArray());
    }
    
    public static NewArrayStatement NewArray(ITypeDefinition type, params object[] parameters)
    {
        return new NewArrayStatement(type, parameters.Select(p => CodeOutputComponent.Get(p)).ToArray());
    }

    public static IndexStatement Index(this IOutputComponent component, object index)
    {
        return new IndexStatement(component, CodeOutputComponent.Get(index));
    }
    
    public static IOutputComponent Increment(object outputComponent)
    {
        return new PostfixOutputComponent("++", CodeOutputComponent.Get(outputComponent));
    }

    public static IOutputComponent Decrement(object outputComponent)
    {
        return new PostfixOutputComponent("--", CodeOutputComponent.Get(outputComponent));
    }

    public static IOutputComponent Await(object outputComponent)
    {
        return new PrefixOutputComponent("await ", CodeOutputComponent.Get(outputComponent));
    }

    public static IOutputComponent Bang(object outputComponent)
    {
        return new PostfixOutputComponent("!", CodeOutputComponent.Get(outputComponent));
    }

    public static IOutputComponent Question(object outputComponent)
    {
        return new PostfixOutputComponent("?", CodeOutputComponent.Get(outputComponent));
    }

    public static IOutputComponent Parenthesis(object value)
    {
        return new WrapStatement(CodeOutputComponent.Get(value), "(", ")");
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

    public static IOutputComponent InvokeGeneric(this IOutputComponent outputComponent, string methodName, IReadOnlyList<ITypeDefinition> genericArgs, params object[] parameters)
    {
        return new CombineOutputComponent(
            outputComponent, 
            new InvokeGenericDefinition(".", methodName, genericArgs, parameters) { Indented = false }
        );
    }

    public static IOutputComponent Invoke(this IOutputComponent outputComponent, string methodName, params object[] parameters)
    {
        return new CombineOutputComponent(
            outputComponent,
            new InvokeDefinition(".", methodName, parameters) { Indented = false }
        );
    }

    public static IOutputComponent Is(IOutputComponent testComponent, ITypeDefinition typeDefinition)
    {
        var statement = new WrapStatement(
            CodeOutputComponent.Get(" is "),
            testComponent,
            CodeOutputComponent.Get(typeDefinition.Name)
        );
        
        statement.AddUsingNamespace(typeDefinition.Namespace);
        
        return statement;
    }
    
    public static StaticPropertyStatement Property(ITypeDefinition typeDefinition, string propertyName)
    {
        return new StaticPropertyStatement(typeDefinition, propertyName) { Indented = false };
    }

    public static IOutputComponent Property(IOutputComponent outputComponent, string propertyName)
    {
        return new LogicStatement(".", outputComponent, propertyName)
        {
            PrintParentheses = false, 
            Indented = false
        };
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
        
    public static IOutputComponent StaticCast(Type type, object value)
    {
        return StaticCast(TypeDefinition.Get(type), value);
    }

    public static IOutputComponent StaticCast(ITypeDefinition typeDefinition, object value)
    {
        return new StaticCastComponent(typeDefinition, value);
    }

    public static IOutputComponent TypeOf(ITypeDefinition typeDefinition)
    {
        return new WrapStatement(new TypeStatement(typeDefinition), "typeof(", ")");
    }

    public static LogicStatement And(params object[] andStatements)
    {
        return new LogicStatement(" && ", andStatements);
    }

    public static LogicStatement Add(params object[] andStatements)
    {
        return new LogicStatement(" + ", andStatements);
    }
        
    public static LogicStatement Subtract(params object[] andStatements)
    {
        return new LogicStatement(" - ", andStatements);
    }
        
    public static LogicStatement Multiply(params object[] andStatements)
    {
        return new LogicStatement(" * ", andStatements);
    }

    public static LogicStatement Divide(params object[] andStatements)
    {
        return new LogicStatement(" / ", andStatements);
    }
        
    public static LogicStatement ConcatSymbol(string symbol, params object[] andStatements)
    {
        return new LogicStatement(symbol, andStatements);
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

    public static LogicStatement NullCoalesce(object leftHandSide, object rightHandSide)
    {
        return new LogicStatement(" ?? ", leftHandSide, rightHandSide);
    }
        
    public static LogicStatement NullCoalesceEqual(object leftHandSide, object rightHandSide)
    {
        return new LogicStatement(" ??= ", leftHandSide, rightHandSide);
    }

    public static WrapStatement YieldReturn(object value)
    {
        return new WrapStatement(CodeOutputComponent.Get(value, false), "yield return ", "");
    }

    public static IOutputComponent ThisInstance()
    {
        return CodeOutputComponent.Get("this");
    }

    public static void WrapInPragma(this BaseOutputComponent baseOutputComponent, params string[] pragma)
    {
        baseOutputComponent.AddLeadingTrait(new PragmaOutputComponent(false,pragma));
        
        baseOutputComponent.AddTrailingTrait(new PragmaOutputComponent(true,pragma));
    }

    public static void EnableNullable(this BaseOutputComponent baseOutputComponent)
    {
        baseOutputComponent.AddLeadingTrait(new NullableEnableComponent(true));
        baseOutputComponent.AddTrailingTrait(new NullableEnableComponent(false));
    }
}