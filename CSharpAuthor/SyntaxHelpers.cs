using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// The expression vocabulary: <c>new</c>, invocations, member access, operators and literals, as
/// components rather than as strings.
/// </summary>
/// <remarks>
/// <para>
/// Every one of these could be written as text through
/// <see cref="BaseBlockDefinition.AddCode(string, object[])"/>, and the reason not to is always the
/// same: a component that mentions a type carries the type, so the file qualifies it, aliases it
/// when its short name is contested, and derives the <c>using</c> it needs. A string carries
/// nothing, and reads the same in a file that qualifies every name as in one that qualifies none.
/// </para>
/// <para>
/// Anywhere one of these takes <c>object</c>, it takes anything the rest of the library takes as a
/// value: a component, an <see cref="ITypeDefinition"/>, a parameter, or a scalar - which is written
/// as the C# literal that denotes it, suffixed and culture-invariant, so <c>1.5f</c> is <c>1.5f</c>
/// and not <c>1.5</c>. A <see cref="string"/> is the exception, and arrives unquoted: it is read as
/// an expression, so wrap it in <see cref="QuoteString"/> for a literal.
/// </para>
/// </remarks>
public static class SyntaxHelpers
{
    /// <inheritdoc cref="New(ITypeDefinition, object[])" />
    public static NewStatement New(Type type, params object[] parameters)
    {
        return New(TypeDefinition.Get(type), parameters);
    }

    /// <summary>
    /// <c>new StringBuilder()</c> - a constructor call, with the type left unrendered so the file
    /// spells it.
    /// </summary>
    /// <remarks>
    /// <paramref name="parameters"/> are the constructor arguments as expressions, so a
    /// <see cref="string"/> arrives unquoted; use <see cref="QuoteString"/> for a literal. A call
    /// with several arguments is broken across lines when
    /// <see cref="OutputContextOptions.BreakInvokeLines"/> is on.
    /// </remarks>
    public static NewStatement New(ITypeDefinition typeDefinition, params object[] parameters)
    {
        return new NewStatement(typeDefinition, parameters);
    }

    /// <inheritdoc cref="NewArray(ITypeDefinition, int)" />
    public static NewArrayStatement NewArray(Type type, int length)
    {
        return NewArray(TypeDefinition.Get(type), length);
    }

    /// <summary>
    /// A sized, empty array: <c>new string[3]</c>.
    /// </summary>
    /// <remarks>
    /// <paramref name="typeDefinition"/> is the <em>element</em> type - pass <c>string</c>, not
    /// <c>string[]</c>. Use the <c>params</c> overload to give the elements instead of a length;
    /// the two are different declarations and only one of them can be both.
    /// </remarks>
    public static NewArrayStatement NewArray(ITypeDefinition typeDefinition, int length)
    {
        return new NewArrayStatement(typeDefinition, length);
    }
    
    /// <inheritdoc cref="NewArray(ITypeDefinition, object[])" />
    public static NewArrayStatement NewArray(Type type, params object[] parameters)
    {
        return new NewArrayStatement(TypeDefinition.Get(type), parameters.Select(p => CodeOutputComponent.Get(p)).ToArray());
    }
    
    /// <summary>
    /// An array with its elements: <c>new string[] { "a", "b" }</c>.
    /// </summary>
    /// <remarks>
    /// <paramref name="type"/> is the element type. The elements are expressions, so strings need
    /// <see cref="QuoteString"/>. Note the overload resolution: a single <c>int</c> argument binds
    /// to <see cref="NewArray(ITypeDefinition, int)"/> and means a length, not a one-element array.
    /// </remarks>
    public static NewArrayStatement NewArray(ITypeDefinition type, params object[] parameters)
    {
        return new NewArrayStatement(type, parameters.Select(p => CodeOutputComponent.Get(p)).ToArray());
    }

    /// <summary>
    /// Indexes into a component: <c>x[0]</c>.
    /// </summary>
    /// <remarks>
    /// The receiver is parenthesised where C# precedence needs it, so indexing the result of a cast
    /// or of an operator reads as intended rather than binding to the last operand.
    /// </remarks>
    public static IndexStatement Index(this IOutputComponent component, object index)
    {
        return new IndexStatement(
            ExpressionPrecedence.AsPrimary(component), CodeOutputComponent.Get(index));
    }
    
    /// <summary>Postfix increment: <c>x++</c>.</summary>
    public static IOutputComponent Increment(object outputComponent)
    {
        return new PostfixOutputComponent("++", CodeOutputComponent.Get(outputComponent));
    }

    /// <summary>Postfix decrement: <c>x--</c>.</summary>
    public static IOutputComponent Decrement(object outputComponent)
    {
        return new PostfixOutputComponent("--", CodeOutputComponent.Get(outputComponent));
    }

    /// <summary>
    /// <c>await x</c>. The enclosing <see cref="MethodDefinition"/> still needs
    /// <see cref="ComponentModifier.Async"/> and a <c>Task</c> return type - neither is inferred.
    /// </summary>
    public static IOutputComponent Await(object outputComponent)
    {
        return new PrefixOutputComponent("await ", CodeOutputComponent.Get(outputComponent));
    }

    /// <summary>
    /// The null-forgiving operator: <c>x!</c>. Suppresses a nullable warning the generator knows is
    /// wrong; it emits no check and does nothing at runtime.
    /// </summary>
    public static IOutputComponent Bang(object outputComponent)
    {
        return new PostfixOutputComponent("!", CodeOutputComponent.Get(outputComponent));
    }

    /// <summary>
    /// A trailing <c>?</c>: <c>x?</c>.
    /// </summary>
    /// <remarks>
    /// A postfix mark on an expression, for hand-composed syntax. It is not the null-conditional
    /// access <c>x?.Y</c>, and it is not how a nullable <em>type</em> is written - that is
    /// <see cref="ITypeDefinition.MakeNullable"/>, which the emitter places itself.
    /// </remarks>
    public static IOutputComponent Question(object outputComponent)
    {
        return new PostfixOutputComponent("?", CodeOutputComponent.Get(outputComponent));
    }

    /// <summary>
    /// Wraps a value in parentheses: <c>(x)</c>. For forcing a grouping the precedence handling
    /// does not already produce.
    /// </summary>
    public static IOutputComponent Parenthesis(object value)
    {
        return new WrapStatement(CodeOutputComponent.Get(value), "(", ")");
    }

    /// <summary>
    /// A static call on a type: <c>Api.Go(1)</c>.
    /// </summary>
    /// <remarks>
    /// The receiver is a type rather than a name, which is the whole reason to use this rather than
    /// <c>AddCode("Api.Go(1)")</c>: the file derives <c>using Sample.Api;</c>, and in
    /// <see cref="TypeOutputMode.Global"/> writes <c>global::Sample.Api.Api.Go(1)</c>. The
    /// arguments are expressions, so strings need <see cref="QuoteString"/>.
    /// </remarks>
    public static StaticInvokeStatement Invoke(ITypeDefinition typeDefinition, string methodName, params object[] parameters)
    {
        return new StaticInvokeStatement(typeDefinition, methodName,
            CodeOutputComponent.GetAll(parameters, false).ToList()) { Indented = false };
    }
        
    /// <inheritdoc cref="InvokeGeneric(ITypeDefinition, string, IReadOnlyList{ITypeDefinition}, object[])" />
    public static StaticInvokeGenericStatement InvokeGeneric(Type type, string methodName, IReadOnlyList<ITypeDefinition> genericArgs, params object[] parameters)
    {
        return new StaticInvokeGenericStatement(TypeDefinition.Get(type), methodName, genericArgs,
            CodeOutputComponent.GetAll(parameters, false).ToList()) { Indented = false };
    }

    /// <summary>
    /// A static call with explicit type arguments: <c>Api.Go&lt;int&gt;(1)</c>.
    /// </summary>
    /// <remarks>
    /// Both the receiver and the type arguments stay types, so all of their namespaces are derived
    /// and all of them are qualified together when the mode qualifies.
    /// </remarks>
    public static StaticInvokeGenericStatement InvokeGeneric(ITypeDefinition typeDefinition, string methodName, IReadOnlyList<ITypeDefinition> genericArgs, params object[] parameters)
    {
        return new StaticInvokeGenericStatement(
            typeDefinition, 
            methodName, 
            genericArgs,
            CodeOutputComponent.GetAll(parameters, false).ToList()) { Indented = false };
    }
        
    /// <inheritdoc cref="Invoke(ITypeDefinition, string, object[])" />
    public static StaticInvokeStatement Invoke(Type type, string methodName, params object[] parameters)
    {
        return new StaticInvokeStatement(TypeDefinition.Get(type), methodName,
                CodeOutputComponent.GetAll(parameters, false).ToList())
            { Indented = false };
    }

    /// <summary>
    /// A call with no receiver and explicit type arguments: <c>Go&lt;int&gt;()</c> - a method on the
    /// type being generated, or one in scope.
    /// </summary>
    public static InvokeGenericDefinition InvokeGeneric(string methodName, IReadOnlyList<ITypeDefinition> genericArgs, params object[] parameters)
    {
        return new InvokeGenericDefinition("", methodName, genericArgs, parameters)
            { Indented = false };
    }
        
    /// <summary>
    /// A call with no receiver: <c>Go(1)</c> - a method on the type being generated, or one in
    /// scope.
    /// </summary>
    /// <remarks>
    /// Note which overload this is: a single <see cref="string"/> first argument is the
    /// <em>method name</em>, not a receiver. The static-call overload takes an
    /// <see cref="ITypeDefinition"/> receiver first.
    /// </remarks>
    public static InvokeDefinition Invoke(string methodName, params object[] parameters)
    {
        return new InvokeDefinition("", methodName, parameters)
            { Indented = false };
    }

    /// <summary>
    /// A generic call on a component receiver, intended as <c>x.Go&lt;int&gt;()</c>.
    /// </summary>
    /// <remarks>
    /// <strong>It emits a doubled dot - <c>x..Go&lt;int&gt;()</c> - which does not compile.</strong>
    /// The non-generic <see cref="Invoke(IOutputComponent, string, object[])"/> guards against the
    /// separator being written twice and this one does not. Use
    /// <see cref="InstanceDefinition.InvokeGeneric"/> on a named receiver, or
    /// <see cref="InvokeGeneric(ITypeDefinition, string, IReadOnlyList{ITypeDefinition}, object[])"/>
    /// for a static call; both write the single dot.
    /// </remarks>
    public static IOutputComponent InvokeGeneric(this IOutputComponent outputComponent, string methodName, IReadOnlyList<ITypeDefinition> genericArgs, params object[] parameters)
    {
        return new CombineOutputComponent(
            ExpressionPrecedence.AsPrimary(outputComponent),
            new InvokeGenericDefinition(".", methodName, genericArgs, parameters) { Indented = false }
        );
    }

    /// <summary>
    /// A call on a component receiver: <c>x.Go(1)</c>.
    /// </summary>
    /// <remarks>
    /// The receiver is parenthesised where C# precedence needs it. Its generic counterpart,
    /// <see cref="InvokeGeneric(IOutputComponent, string, IReadOnlyList{ITypeDefinition}, object[])"/>,
    /// emits a doubled dot and should not be used.
    /// </remarks>
    public static IOutputComponent Invoke(this IOutputComponent outputComponent, string methodName, params object[] parameters)
    {
        return new CombineOutputComponent(
            ExpressionPrecedence.AsPrimary(outputComponent),
            new InvokeDefinition(".", methodName, parameters) { Indented = false }
        );
    }

    /// <summary>
    /// <c>value is SomeType</c>.
    /// </summary>
    /// <remarks>
    /// The type is written through <see cref="IOutputContext.Write(ITypeDefinition)"/> rather than
    /// flattened to its <see cref="ITypeDefinition.Name"/> here. Taking the name at build time drops
    /// everything the name is not - the generic arguments, the array shape, the containing type - and
    /// pins the test to short-name output, so a file emitted in <see cref="TypeOutputMode.Global"/>
    /// got an unqualified name propped up by a using it also had no reason to hold.
    /// </remarks>
    public static IOutputComponent Is(IOutputComponent testComponent, ITypeDefinition typeDefinition)
    {
        return new WrapStatement(
            CodeOutputComponent.Get(" is "),
            testComponent,
            new TypeStatement(typeDefinition)
        );
    }
    
    /// <summary>
    /// A static member reached off a type: <c>Api.Default</c>.
    /// </summary>
    /// <remarks>
    /// The receiver stays a type, so the namespace is derived and the name is qualified when the
    /// mode qualifies. Written as the string <c>"Api.Default"</c> it would be neither, which is the
    /// failure this exists to remove.
    /// </remarks>
    public static StaticPropertyStatement Property(ITypeDefinition typeDefinition, string propertyName)
    {
        return new StaticPropertyStatement(typeDefinition, propertyName) { Indented = false };
    }

    /// <summary>
    /// A member reached off a component: <c>x.Name</c>.
    /// </summary>
    /// <remarks>
    /// The receiver is parenthesised where C# precedence needs it. For a receiver that is a plain
    /// name, <see cref="InstanceDefinition.Property"/> is shorter and gives back another instance
    /// that can be walked further.
    /// </remarks>
    public static IOutputComponent Property(IOutputComponent outputComponent, string propertyName)
    {
        return new LogicStatement(".", ExpressionPrecedence.AsPrimary(outputComponent), propertyName)
        {
            PrintParentheses = false, 
            Indented = false
        };
    }

    /// <summary>
    /// A constructor initialiser: <c>: base(name)</c>, for
    /// <see cref="ClassDefinition.AddConstructor"/>.
    /// </summary>
    /// <remarks>
    /// Not the route for a record's positional base - that is
    /// <see cref="ClassDefinition.AddBaseType(ITypeDefinition, IOutputComponent[])"/>, where the
    /// arguments belong to the base type clause rather than to a constructor.
    /// </remarks>
    public static BaseStatement Base(params object[] parameters)
    {
        var statements = CodeOutputComponent.GetAll(parameters);

        return new BaseStatement(statements.ToList());
    }

    /// <summary>
    /// A constructor initialiser chaining to another overload: <c>this(1)</c>.
    /// </summary>
    /// <remarks>
    /// For the current instance as a value - <c>this</c> on its own - use
    /// <see cref="ThisInstance"/>; this one always writes the call parentheses.
    /// </remarks>
    public static IOutputComponent This(params object[] parameters)
    {
        var statements = CodeOutputComponent.GetAll(parameters);

        return new WrapStatement(new ListOutputComponent(statements.ToList()), "this(", ")");
    }

    /// <summary>
    /// <paramref name="stringValue"/> as a C# string literal.
    /// </summary>
    /// <remarks>
    /// The content is escaped. Concatenating quotes around the raw value, which is what this did,
    /// turned any value holding a quote or a backslash into a syntax error - <c>he said "hi"</c>
    /// came out as <c>"he said "hi""</c>, CS1002 - and any value holding a newline into a file that
    /// stopped parsing at that line.
    /// </remarks>
    public static string QuoteString(string stringValue)
    {
        return LiteralFormatter.QuoteString(stringValue);
    }

    /// <summary>
    /// The literal <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Needed because a C# <c>null</c> argument means "no value given" everywhere in this library -
    /// <see cref="BaseBlockDefinition.Return"/> with null is a bare <c>return;</c>, not
    /// <c>return null;</c>. This is how the value is said out loud.
    /// </remarks>
    /// <inheritdoc cref="FieldKeywordComponent"/>
    /// <param name="backingFieldName">
    /// What to write below C# 14, where there is no keyword. The caller declares this field.
    /// </param>
    /// <param name="context">The property's name, for the diagnostic.</param>
    public static IOutputComponent Field(string backingFieldName, string? context = null)
    {
        return new FieldKeywordComponent(backingFieldName, context) { Indented = false };
    }

    public static IOutputComponent Null()
    {
        return CodeOutputComponent.Get("null");
    }
        
    /// <inheritdoc cref="StaticCast(ITypeDefinition, object)" />
    public static IOutputComponent StaticCast(Type type, object value)
    {
        return StaticCast(TypeDefinition.Get(type), value);
    }

    /// <summary>
    /// A cast: <c>(int)x</c>.
    /// </summary>
    /// <remarks>
    /// The target stays a type, so it is qualified with everything else - <c>(global::Sample.Api.Api)x</c>
    /// in a qualifying mode. "Static" here means the C# cast operator, as opposed to an <c>as</c> or
    /// a pattern test; see <see cref="Is"/> for the latter.
    /// </remarks>
    public static IOutputComponent StaticCast(ITypeDefinition typeDefinition, object value)
    {
        return new StaticCastComponent(typeDefinition, value);
    }

    /// <summary>
    /// <c>typeof(Api)</c> - the type as a runtime value.
    /// </summary>
    /// <remarks>
    /// The usual way to pass a type to an attribute, where a raw string would be a bare identifier
    /// with no namespace behind it.
    /// </remarks>
    public static IOutputComponent TypeOf(ITypeDefinition typeDefinition)
    {
        return new WrapStatement(new TypeStatement(typeDefinition), "typeof(", ")");
    }

    /// <summary>
    /// <c>(x &amp;&amp; y)</c>, over any number of operands.
    /// </summary>
    /// <remarks>
    /// Parenthesised, which is what makes these safe to nest. Passed straight to
    /// <see cref="BaseBlockDefinition.If(IOutputComponent)"/> the outer parentheses are dropped,
    /// because the <c>if</c> supplies its own.
    /// </remarks>
    public static LogicStatement And(params object[] andStatements)
    {
        return new LogicStatement(" && ", andStatements);
    }

    /// <summary><c>(1 + 2)</c>, over any number of operands.</summary>
    public static LogicStatement Add(params object[] andStatements)
    {
        return new LogicStatement(" + ", andStatements);
    }
        
    /// <summary><c>(3 - 1)</c>, over any number of operands.</summary>
    public static LogicStatement Subtract(params object[] andStatements)
    {
        return new LogicStatement(" - ", andStatements);
    }
        
    /// <summary><c>(2 * 3)</c>, over any number of operands.</summary>
    public static LogicStatement Multiply(params object[] andStatements)
    {
        return new LogicStatement(" * ", andStatements);
    }

    /// <summary><c>(6 / 2)</c>, over any number of operands.</summary>
    public static LogicStatement Divide(params object[] andStatements)
    {
        return new LogicStatement(" / ", andStatements);
    }
        
    /// <summary>
    /// The operands joined by <paramref name="symbol"/> and parenthesised - the general form the
    /// named operators are built on, for one that has no method here.
    /// </summary>
    /// <remarks>
    /// The symbol is written verbatim and needs its own surrounding spaces:
    /// <c>ConcatSymbol(" % ", 5, 2)</c> is <c>(5 % 2)</c>.
    /// </remarks>
    public static LogicStatement ConcatSymbol(string symbol, params object[] andStatements)
    {
        return new LogicStatement(symbol, andStatements);
    }

    /// <inheritdoc cref="And(object[])" />
    /// <remarks>The overload for a list a caller has already built - a condition per validated
    /// member, say.</remarks>
    public static LogicStatement And(IReadOnlyList<IOutputComponent> andStatements)
    {
        return new LogicStatement(" && ", andStatements);
    }

    /// <summary><c>(x || y)</c>, over any number of operands.</summary>
    public static LogicStatement Or(params object[] orStatements)
    {
        return new LogicStatement(" || ", orStatements);
    }

    /// <inheritdoc cref="Or(object[])" />
    /// <remarks>The overload for a list a caller has already built.</remarks>
    public static LogicStatement Or(IReadOnlyList<IOutputComponent> orStatements)
    {
        return new LogicStatement(" || ", orStatements);
    }

    /// <summary>
    /// <c>(x == 1)</c>. Named with the postfix because <c>Equals</c> is taken by
    /// <see cref="object.Equals(object)"/>.
    /// </summary>
    public static LogicStatement EqualsStatement(object leftHandSide, object rightHandSide)
    {
        return new LogicStatement(" == ", leftHandSide, rightHandSide);
    }

    /// <summary><c>(x != 1)</c>.</summary>
    public static LogicStatement NotEquals(object leftHandSide, object rightHandSide)
    {
        return new LogicStatement(" != ", leftHandSide, rightHandSide);
    }

    /// <summary><c>(x &gt; 1)</c>.</summary>
    public static LogicStatement GreaterThan(object leftHandSide, object rightHandSide)
    {
        return new LogicStatement(" > ", leftHandSide, rightHandSide);
    }
    /// <summary><c>(x &lt; 1)</c>.</summary>
    public static LogicStatement LessThan(object leftHandSide, object rightHandSide)
    {
        return new LogicStatement(" < ", leftHandSide, rightHandSide);
    }

    /// <summary><c>(x &gt;= 1)</c>.</summary>
    public static LogicStatement GreaterThanOrEquals(object leftHandSide, object rightHandSide)
    {
        return new LogicStatement(" >= ", leftHandSide, rightHandSide);
    }
    /// <summary><c>(x &lt;= 1)</c>.</summary>
    public static LogicStatement LessThanOrEquals(object leftHandSide, object rightHandSide)
    {
        return new LogicStatement(" <= ", leftHandSide, rightHandSide);
    }

    /// <summary><c>(x ?? 1)</c> - the right-hand side when the left is null.</summary>
    public static LogicStatement NullCoalesce(object leftHandSide, object rightHandSide)
    {
        return new LogicStatement(" ?? ", leftHandSide, rightHandSide);
    }
        
    /// <summary><c>(x ??= 1)</c> - assigns only if the left is null. C# 8.</summary>
    public static LogicStatement NullCoalesceEqual(object leftHandSide, object rightHandSide)
    {
        return new LogicStatement(" ??= ", leftHandSide, rightHandSide);
    }

    /// <summary>
    /// <c>yield return x</c>, for a method whose body is an iterator.
    /// </summary>
    /// <remarks>
    /// The return type still has to be <c>IEnumerable&lt;T&gt;</c> or <c>IEnumerator&lt;T&gt;</c>;
    /// nothing infers it from the presence of this.
    /// </remarks>
    public static WrapStatement YieldReturn(object value)
    {
        return new WrapStatement(CodeOutputComponent.Get(value, false), "yield return ", "");
    }

    /// <summary>
    /// The current instance as a value: <c>this</c>.
    /// </summary>
    /// <remarks>
    /// Left unescaped, unlike an ordinary identifier - it arrives as an expression rather than as a
    /// name, and <c>@this</c> would not mean the same thing.
    /// </remarks>
    public static IOutputComponent ThisInstance()
    {
        return CodeOutputComponent.Get("this");
    }

    /// <summary>
    /// Surrounds the declaration with <c>#pragma warning disable</c> and its matching restore.
    /// </summary>
    /// <remarks>
    /// A leading and a trailing trait added as a pair, so the restore cannot be forgotten and the
    /// suppression cannot leak past the declaration into code the generator did not write.
    /// </remarks>
    public static void WrapInPragma(this BaseOutputComponent baseOutputComponent, params string[] pragma)
    {
        baseOutputComponent.AddLeadingTrait(new PragmaOutputComponent(false,pragma));
        
        baseOutputComponent.AddTrailingTrait(new PragmaOutputComponent(true,pragma));
    }

    /// <summary>
    /// Surrounds the declaration with <c>#nullable enable</c> and its restore.
    /// </summary>
    /// <remarks>
    /// For a generated file whose annotations should be honoured whatever the consuming project's
    /// setting is. Scoped to the declaration for the same reason
    /// <see cref="WrapInPragma"/> is: the directive is restored rather than left in force.
    /// </remarks>
    public static void EnableNullable(this BaseOutputComponent baseOutputComponent)
    {
        baseOutputComponent.AddLeadingTrait(new NullableEnableComponent(true));
        baseOutputComponent.AddTrailingTrait(new NullableEnableComponent(false));
    }
}