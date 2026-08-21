using System;
using System.Collections.Generic;
using System.Globalization;
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

    /// <summary>
    /// The null-forgiving operator, <c>x!</c> - <em>not</em> logical negation. For <c>!x</c> use
    /// <see cref="Not"/>.
    /// </summary>
    /// <remarks>
    /// Under <c>#nullable enable</c> both <c>x!</c> and <c>!x</c> are legal wherever a bool is
    /// expected, so choosing the wrong one produces a condition that compiles and tests the
    /// opposite thing.
    /// </remarks>
    public static IOutputComponent Bang(object outputComponent)
    {
        return new PostfixOutputComponent("!", CodeOutputComponent.Get(outputComponent));
    }

    /// <summary>
    /// Logical negation, <c>!x</c>.
    /// </summary>
    /// <remarks>
    /// A <see cref="LogicStatement"/> writes its own parentheses, so negating one yields
    /// <c>!(a &amp;&amp; b)</c> rather than the <c>!a &amp;&amp; b</c> that would change meaning.
    /// </remarks>
    public static IOutputComponent Not(object outputComponent)
    {
        return new PrefixOutputComponent("!", CodeOutputComponent.Get(outputComponent));
    }

    /// <summary>
    /// A trailing <c>?</c> - the nullable annotation on a type, not the conditional operator.
    /// </summary>
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

    /// <inheritdoc cref="MemberAccess"/>
    public static IOutputComponent Property(IOutputComponent outputComponent, string propertyName)
    {
        return MemberAccess(outputComponent, propertyName);
    }

    /// <summary>
    /// Reaching a member off something - <c>target.Name</c>, for a property, field or anything
    /// else named.
    /// </summary>
    /// <remarks>
    /// The same thing <see cref="Property(IOutputComponent,string)"/> has always done, under the
    /// name that says so. Building it out of a string instead - <c>Code(target + "." + name)</c> -
    /// costs the target its type reference, and with it the import that reference would have
    /// derived.
    /// </remarks>
    public static IOutputComponent MemberAccess(object target, string memberName)
    {
        return new LogicStatement(".", CodeOutputComponent.Get(target), memberName)
        {
            PrintParentheses = false,
            Indented = false
        };
    }

    /// <summary>
    /// The null test, <c>x is null</c>.
    /// </summary>
    public static IOutputComponent IsNull(object outputComponent)
    {
        return new PostfixOutputComponent(" is null", CodeOutputComponent.Get(outputComponent));
    }

    /// <summary>
    /// The negated null test, <c>x is not null</c>. A C# 9 pattern.
    /// </summary>
    public static IOutputComponent IsNotNull(object outputComponent)
    {
        return new PostfixOutputComponent(" is not null", CodeOutputComponent.Get(outputComponent));
    }

    /// <summary>
    /// A <c>ref</c> argument at a call site. The <c>ref</c> on a parameter <em>declaration</em> is
    /// <see cref="ParameterModifier"/>.
    /// </summary>
    public static IOutputComponent Ref(object outputComponent)
    {
        return new PrefixOutputComponent(KeyWords.Ref + " ", CodeOutputComponent.Get(outputComponent));
    }

    /// <inheritdoc cref="Ref"/>
    public static IOutputComponent Out(object outputComponent)
    {
        return new PrefixOutputComponent(KeyWords.Out + " ", CodeOutputComponent.Get(outputComponent));
    }

    /// <inheritdoc cref="Ref"/>
    public static IOutputComponent In(object outputComponent)
    {
        return new PrefixOutputComponent(KeyWords.In + " ", CodeOutputComponent.Get(outputComponent));
    }

    /// <summary>
    /// An <c>out</c> argument that declares its variable - <c>out var name</c>, or
    /// <c>out Widget name</c> when a type is given.
    /// </summary>
    public static IOutputComponent OutVar(string name, ITypeDefinition? variableType = null)
    {
        if (variableType == null)
        {
            return CodeOutputComponent.Get($"{KeyWords.Out} {KeyWords.Var} {name}");
        }

        return new CombineOutputComponent(
            CodeOutputComponent.Get($"{KeyWords.Out} "),
            new TypeStatement(variableType),
            CodeOutputComponent.Get(" " + name));
    }

    /// <summary>
    /// <c>nameof(x)</c>.
    /// </summary>
    public static IOutputComponent NameOf(object outputComponent)
    {
        return new WrapStatement(CodeOutputComponent.Get(outputComponent), "nameof(", ")");
    }

    /// <inheritdoc cref="NameOf(object)"/>
    public static IOutputComponent NameOf(ITypeDefinition typeDefinition)
    {
        return new WrapStatement(new TypeStatement(typeDefinition), "nameof(", ")");
    }

    /// <summary>
    /// The conditional operator, <c>condition ? whenTrue : whenFalse</c>.
    /// </summary>
    public static ConditionalStatement Conditional(object condition, object whenTrue, object whenFalse)
    {
        return new ConditionalStatement(condition, whenTrue, whenFalse) { Indented = false };
    }

    /// <summary>
    /// <c>default</c>, or <c>default(T)</c> where a type is given.
    /// </summary>
    public static IOutputComponent Default(ITypeDefinition? typeDefinition = null)
    {
        if (typeDefinition == null)
        {
            return CodeOutputComponent.Get("default");
        }

        return new WrapStatement(new TypeStatement(typeDefinition), "default(", ")");
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

    /// <summary>
    /// Wraps a value in double quotes as a C# string literal, escaping whatever it contains.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The quotes used to be added without escaping, so a value carrying a <c>"</c>, a backslash
    /// or a line break closed the literal early and broke the consumer's build. Generators rarely
    /// see these values - they arrive from attribute arguments and symbol names in user code -
    /// which made it a bug the generator author could not reproduce.
    /// </para>
    /// <para>
    /// Escaped as a regular literal rather than a verbatim one, because a verbatim literal cannot
    /// carry a line break without also changing the value.
    /// </para>
    /// </remarks>
    public static string QuoteString(string? stringValue)
    {
        if (string.IsNullOrEmpty(stringValue))
        {
            return "\"\"";
        }

        var builder = new StringBuilder(stringValue!.Length + 2);

        builder.Append('"');

        foreach (var character in stringValue)
        {
            AppendEscaped(builder, character);
        }

        builder.Append('"');

        return builder.ToString();
    }

    private static void AppendEscaped(StringBuilder builder, char character)
    {
        switch (character)
        {
            case '"': builder.Append("\\\""); return;
            case '\\': builder.Append("\\\\"); return;
            case '\0': builder.Append("\\0"); return;
            case '\a': builder.Append("\\a"); return;
            case '\b': builder.Append("\\b"); return;
            case '\f': builder.Append("\\f"); return;
            case '\n': builder.Append("\\n"); return;
            case '\r': builder.Append("\\r"); return;
            case '\t': builder.Append("\\t"); return;
            case '\v': builder.Append("\\v"); return;
        }

        // The compiler ends a literal at any of these, so they cannot be written through even
        // though they are neither control characters nor quotes.
        var isLineTerminator = character is '\u0085' or '\u2028' or '\u2029';

        // Surrogates go out as escapes so a lone one - which has no UTF-8 encoding - still
        // survives being written to a file. A valid pair escapes to the same pair, so nothing
        // is lost by not telling them apart.
        if (isLineTerminator || char.IsControl(character) || char.IsSurrogate(character))
        {
            builder.Append("\\u");
            builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));

            return;
        }

        builder.Append(character);
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