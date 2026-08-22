using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// A <c>{ }</c> block and the statements in it - a method body, a loop body, the arm of an
/// <c>if</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every construct that holds statements derives from this, so the same calls build a method body
/// and the body of a loop inside it. The nesting is the return value: <see cref="If(string)"/>
/// hands back a block, and statements added to <em>that</em> are inside the <c>if</c>.
/// </para>
/// <example>
/// <code>
/// var loop = method.For("i", 0, 10);
/// loop.If("i == 3").Continue();
/// method.Return("0");
/// </code>
/// which is
/// <code>
/// for(var i = 0; i &lt; 10; i++)
/// {
///     if (i == 3)
///     {
///         continue;
///     }
/// }
/// return 0;
/// </code>
/// </example>
/// <para>
/// Prefer the structured calls over writing the same construct as text through
/// <see cref="AddCode(string, object[])"/>: the indent is managed, the brace style follows
/// <see cref="OutputContextOptions.BraceStyle"/>, and any type mentioned reaches the file as a type
/// rather than as a name that may not resolve.
/// </para>
/// </remarks>
public abstract class BaseBlockDefinition : BaseOutputComponent
{
    protected readonly List<IOutputComponent> StatementList = new ();

    /// <summary>
    /// How many statements the block holds. Zero is an empty <c>{ }</c> - and, on a
    /// <see cref="MethodDefinition"/>, is also what makes an interface member bodyless and a
    /// property an auto-property.
    /// </summary>
    public int StatementCount => StatementList.Count;

    /// <summary>
    /// Appends a component built elsewhere, returning it unchanged so the call can be inlined.
    /// </summary>
    /// <remarks>
    /// The escape hatch, and what every other method here is built on. The component decides its
    /// own layout: it is added exactly as given, with no indent and no <c>;</c> added around it.
    /// Use <see cref="AddIndentedStatement"/> for something that should be a statement on a line of
    /// its own.
    /// </remarks>
    public T Add<T>(T component) where T : IOutputComponent
    {
        StatementList.Add(component);

        return component;
    }

    /// <summary>
    /// One statement, on its own line, at the block's indent, terminated with <c>;</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the one to reach for. It takes a string or any component, and it writes the three
    /// things a statement needs and a raw string does not: the indent, the semicolon, and the line
    /// break.
    /// </para>
    /// <example>
    /// <code>
    /// method.AddIndentedStatement("var x = 1");
    /// // var x = 1;
    ///
    /// method.AddIndentedStatement(SyntaxHelpers.Invoke(consoleType, "Write", SyntaxHelpers.QuoteString("hi")));
    /// // Console2.Write("hi");   - and the file gets the using for consoleType
    /// </code>
    /// Note that the text carries no <c>;</c> of its own. Passing <c>"var x = 1;"</c> gives
    /// <c>var x = 1;;</c>, which compiles as an empty statement and reads as a typo.
    /// </example>
    /// <para>
    /// The difference from <see cref="AddCode(string, object[])"/> is exactly this: <c>AddCode</c>
    /// writes the text as given, so the caller owns the semicolon and any continuation lines.
    /// </para>
    /// </remarks>
    public virtual object AddIndentedStatement(object component)
    {
        StatementList.Add(new IndentedStatementComponent(CodeOutputComponent.Get( component)));

        return component;
    }

    /// <summary>
    /// A statement with types substituted into it, held as pieces so the types are still types when
    /// the file is serialized.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <c>{argN}</c> used to be replaced with the type's short name on the spot, which fixed the
    /// text before anything knew what mode the file would be written in or what else it would
    /// contain. The name went in unqualified even in a file that qualified everything, and the
    /// namespace was declared on the side to make it resolve. The pieces keep the type instead:
    /// it is rendered with everything else, at the end.
    ///
    /// A <c>[argN]</c> is still substituted here, because it is text by definition.
    /// </para>
    /// <para>
    /// <strong>The two placeholders are not spellings of one thing, and the signature does not say
    /// so.</strong> <paramref name="types"/> is positional either way - the first value fills
    /// <c>{arg1}</c> or <c>[arg1]</c>, the second <c>{arg2}</c> - but the brackets decide what
    /// happens to it:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <c>{argN}</c> keeps an <see cref="ITypeDefinition"/> as a type. It is qualified by whatever
    /// <see cref="OutputContextOptions.TypeOutputMode"/> is in force, aliased if its short name is
    /// contested, and its namespace is added to the file's <c>using</c> list. A value that is not a
    /// type is formatted as the C# expression that denotes it, and an <c>enum</c> arrives as
    /// <c>Type.Member</c> with the type still a type.
    /// </description></item>
    /// <item><description>
    /// <c>[argN]</c> substitutes text, on the spot. A type becomes
    /// <see cref="object.ToString"/> - the 1.x identity string, not the C# name - and no
    /// <c>using</c> is derived for it, because nothing recorded that a type was ever mentioned. An
    /// <c>enum</c> arrives as the bare member name, which is CS0103 unless something of that name
    /// happens to be in scope, in which case it compiles and means something else. For a value
    /// that is neither a type nor an <c>enum</c>, the two spellings agree.
    /// </description></item>
    /// </list>
    /// <para>
    /// <strong>A <see cref="string"/> value is code, not a literal.</strong>
    /// <c>AddCode("var s = {arg1};", "hello")</c> emits <c>var s = hello;</c> - an identifier
    /// reference - because throughout this library a string is a fragment of C#. That is the rule
    /// <see cref="LiteralFormatter.Format"/> states and every other entry point already followed;
    /// <c>{argN}</c> was the one place that did not. For a string <em>literal</em>, ask for one:
    /// <c>AddCode("var s = {arg1};", SyntaxHelpers.QuoteString("hello"))</c> emits
    /// <c>var s = "hello";</c>.
    /// </para>
    /// <example>
    /// The same call, one character apart:
    /// <code>
    /// method.AddCode("var list = new {arg1}();", TypeDefinition.List(typeof(string)));
    /// // ShortName:  var list = new List&lt;string&gt;();   + using System.Collections.Generic;
    /// // Global:     var list = new global::System.Collections.Generic.List&lt;string&gt;();
    ///
    /// method.AddCode("var list = new [arg1]();", TypeDefinition.List(typeof(string)));
    /// // both modes: var list = new System.Collections.Generic.List&lt;.string&gt;();   and no using
    /// </code>
    /// The second is not a worse spelling of the first. It does not compile, it is the same wrong
    /// text in every output mode, and nothing reports it.
    /// </example>
    /// <para>
    /// So: <c>{argN}</c> for anything that is a type or a value. <c>[argN]</c> only for text that is
    /// text - an identifier, an operator, a fragment of a name being built up.
    /// </para>
    /// <para>
    /// Every occurrence of a placeholder is replaced, so <c>{arg1}</c> can be used twice with one
    /// value. Where both spellings of the same index appear in one statement, <c>{argN}</c> is the
    /// one that is filled and the <c>[argN]</c> is left in the text.
    /// </para>
    /// <para>
    /// A placeholder with no matching value is left in the output verbatim, and a value with no
    /// matching placeholder is ignored. Neither is reported: the count is not checked, so a
    /// mismatch reaches the generated file as a literal <c>{arg2}</c>.
    /// </para>
    /// <para>
    /// The statement is written at the block's indent and followed by a line break, but the text is
    /// otherwise written exactly as given - no <c>;</c> is added, and a <c>\n</c> inside it starts a
    /// line that is <em>not</em> indented. That is what makes this the wrong tool for a multi-line
    /// construct: <see cref="If(string)"/>, <see cref="ForEach"/> and <see cref="For(string, object, object)"/>
    /// manage the indent and honour <see cref="OutputContextOptions.BraceStyle"/>; hand-written
    /// braces do neither. Use <see cref="AddIndentedStatement"/> for a single statement that should
    /// end in <c>;</c>.
    /// </para>
    /// </remarks>
    /// <param name="statement">The statement text, with <c>{argN}</c> and <c>[argN]</c>
    /// placeholders.</param>
    /// <param name="types">Values for the placeholders, in order. A <see cref="Type"/> is converted
    /// to an <see cref="ITypeDefinition"/> for you.</param>
    public virtual CodeOutputComponent AddCode(string statement, params object[] types)
    {
        var parts = new List<object> { statement };

        if (types is { Length: > 0 })
        {
            for (var index = 0; index < types.Length; index++)
            {
                var value = types[index];
                var typeSwapString =
                    "{arg" + (index + 1).ToString(CultureInfo.InvariantCulture) + "}";

                if (PartsContain(parts, typeSwapString))
                {
                    if (value is Type typeValue)
                    {
                        value = TypeDefinition.Get(typeValue);
                    }

                    ReplaceInParts(parts, typeSwapString, GetSubstitutionParts(value));
                }
                else
                {
                    var rawSwapString =
                        "[arg" + (index + 1).ToString(CultureInfo.InvariantCulture) + "]";

                    if (PartsContain(parts, rawSwapString))
                    {
                        ReplaceInParts(
                            parts, rawSwapString, new object[] { LiteralFormatter.Format(value) });
                    }
                }
            }
        }

        return Add(CodeOutputComponent.FromParts(parts));
    }

    private static bool PartsContain(List<object> parts, string marker)
    {
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i] is string text && text.IndexOf(marker, StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The pieces a <c>{argN}</c> substitution becomes - more than one where the value is written
    /// as a type plus text.
    /// </summary>
    /// <remarks>
    /// An enum value is the case that needs two: it is written as <c>Type.Member</c>, and the
    /// <c>Type</c> half has to stay a type so the file derives its namespace. Handed over as text it
    /// was the member name alone - <c>var x = Singleton;</c>, CS0103 - which is the section 1
    /// defect reached through <c>AddCode</c> rather than through a raw string.
    /// </remarks>
    private IReadOnlyList<object> GetSubstitutionParts(object value)
    {
        if (value is ITypeDefinition typeDefinition)
        {
            return new object[] { typeDefinition };
        }

        if (value is Enum && CodeOutputComponent.Get(value) is CodeOutputComponent component)
        {
            return component.Parts ?? new object[] { GetObjectStringValue(value) };
        }

        return new object[] { GetObjectStringValue(value) };
    }

    private static void ReplaceInParts(
        List<object> parts, string marker, IReadOnlyList<object> replacements)
    {
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i] is not string text)
            {
                continue;
            }

            var index = text.IndexOf(marker, StringComparison.Ordinal);

            if (index < 0)
            {
                continue;
            }

            var replaced = new List<object>();
            var position = 0;

            while (index >= 0)
            {
                if (index > position)
                {
                    replaced.Add(text.Substring(position, index - position));
                }

                for (var r = 0; r < replacements.Count; r++)
                {
                    replaced.Add(replacements[r]);
                }

                position = index + marker.Length;
                index = text.IndexOf(marker, position, StringComparison.Ordinal);
            }

            if (position < text.Length)
            {
                replaced.Add(text.Substring(position));
            }

            parts.RemoveAt(i);
            parts.InsertRange(i, replaced);

            i += replaced.Count - 1;
        }
    }

    /// <summary>
    /// The text a substituted value becomes. A type never reaches here.
    /// </summary>
    /// <remarks>
    /// It used to: a type was turned into its short name at this point, which is when the tree is
    /// being built and before any output mode exists. There is no answer to give then - the same
    /// tree is meant to be writable as <c>Result</c> or as <c>global::Sample.Models.Result</c> - so
    /// the caller keeps the type and this only ever sees things that really are values.
    /// </remarks>
    private string GetObjectStringValue(object value)
    {
        return LiteralFormatter.Format(value);
    }

    /// <summary>
    /// A <c>switch</c> statement over <paramref name="switchValue"/>.
    /// </summary>
    /// <remarks>
    /// Cases are added to the block this returns, with
    /// <see cref="SwitchBlockDefinition.AddCase"/> and
    /// <see cref="SwitchBlockDefinition.AddDefault"/>; each of those returns a block for that arm's
    /// statements. Nothing adds the <c>break</c>, so an arm that falls through needs one - a
    /// <see cref="Return"/> or a <see cref="Break"/> of its own.
    /// </remarks>
    public SwitchBlockDefinition Switch(object switchValue)
    {
        var switchStatement = new SwitchBlockDefinition(CodeOutputComponent.Get(switchValue));

        StatementList.Add(switchStatement);

        return switchStatement;
    }

    /// <summary>
    /// A blank line, for separating groups of statements in generated code that a person will read.
    /// </summary>
    public virtual void NewLine()
    {
        AddCode("");
    }

    /// <summary>
    /// A <c>try</c> block. Add <c>catch</c> clauses and a <c>finally</c> to what it returns.
    /// </summary>
    /// <remarks>
    /// <example>
    /// <code>
    /// var attempt = method.Try();
    /// attempt.AddIndentedStatement("Work()");
    /// attempt.Catch(TypeDefinition.Get(typeof(Exception)), "e").AddIndentedStatement("Log(e)");
    /// </code>
    /// which is
    /// <code>
    /// try
    /// {
    ///     Work();
    /// }
    /// catch (Exception e)
    /// {
    ///     Log(e);
    /// }
    /// </code>
    /// </example>
    /// Statements added to the <see cref="TryCatchBlock"/> itself are the <c>try</c> body; each
    /// <see cref="TryCatchBlock.Catch(ITypeDefinition, string, IOutputComponent)"/> and
    /// <see cref="TryCatchBlock.Finally"/> returns a block of its own.
    /// </remarks>
    public TryCatchBlock Try()
    {
        return Add(new TryCatchBlock());
    }

    /// <inheritdoc cref="Throw(ITypeDefinition, object[])" />
    public void Throw(Type type, params object[] parameters)
    {
        Add(new PostfixOutputComponent(";\n", new ThrowNewExceptionStatement(TypeDefinition.Get(type), parameters)));
    }

    /// <summary>
    /// <c>throw new ArgumentNullException("name");</c> - constructing the exception and throwing it
    /// in one statement.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="parameters"/> are the constructor's arguments as <em>expressions</em>, the
    /// same way <see cref="BaseOutputComponent.AddAttribute(ITypeDefinition, object[])"/> takes
    /// them, so a <see cref="string"/> arrives unquoted:
    /// <c>Throw(typeof(ArgumentNullException), "name")</c> emits
    /// <c>throw new ArgumentNullException(name);</c>, which names whatever <c>name</c> is in scope.
    /// Wrap it in <see cref="SyntaxHelpers.QuoteString"/> for a literal.
    /// </para>
    /// <para>
    /// There is no overload that rethrows: a bare <c>throw;</c> inside a <c>catch</c> is
    /// <c>AddIndentedStatement("throw")</c>.
    /// </para>
    /// </remarks>
    public void Throw(ITypeDefinition exceptionType, params object[] parameters)
    {
        Add(new PostfixOutputComponent(";\n", new ThrowNewExceptionStatement(exceptionType, parameters)));
    }

    /// <summary>
    /// <c>return value;</c>, or a bare <c>return;</c> when nothing is given.
    /// </summary>
    /// <remarks>
    /// <paramref name="returnValue"/> takes anything the rest of the library takes as a value - a
    /// string of C#, a component, a parameter, a literal - so
    /// <c>method.Return(SyntaxHelpers.QuoteString("hi"))</c> returns a string literal and
    /// <c>method.Return("hi")</c> returns whatever <c>hi</c> names in scope. A null argument is a
    /// bare <c>return;</c>, not <c>return null;</c>; for that, pass
    /// <see cref="SyntaxHelpers.Null"/>.
    /// </remarks>
    public void Return(object? returnValue = null)
    {
        if (returnValue == null)
        {
            AddIndentedStatement("return");
        }
        else
        {
            AddIndentedStatement(
                new AppendStatement("return ", CodeOutputComponent.Get(returnValue)));
        }
    }

    /// <summary>
    /// <c>break;</c> - leaves the enclosing loop or <c>switch</c> arm.
    /// </summary>
    /// <remarks>
    /// Which construct it leaves is decided by where it was added, so add it to the block the loop
    /// returned rather than to the method: <c>loop.If("i == 5").Break()</c> breaks the loop,
    /// <c>method.Break()</c> emits a <c>break</c> with nothing to break out of.
    /// </remarks>
    public void Break()
    {
        AddIndentedStatement("break");
    }

    /// <summary>
    /// <c>continue;</c> - the other half of <see cref="Break"/>, which had no equivalent, so the
    /// only way to skip an iteration was to write the statement out as text.
    /// </summary>
    public void Continue()
    {
        AddIndentedStatement("continue");
    }

    /// <summary>
    /// <c>while(x &gt; 0) { }</c>. The body is the block this returns.
    /// </summary>
    /// <remarks>
    /// <paramref name="testStatement"/> is written inside the parentheses as given. There is no
    /// <c>do/while</c>; that shape has to be built as text.
    /// </remarks>
    public WhileDefinition While(object testStatement)
    {
        return Add(new WhileDefinition(testStatement));
    }

    /// <summary>
    /// <c>foreach(var name in names) { }</c>. The body is the block this returns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The loop variable is always declared <c>var</c>, and its name is the one thing this takes as
    /// a string. Read it back off <see cref="ForEachDefinition.Instance"/> to use it in the body,
    /// rather than repeating the name and having to keep the two in step.
    /// </para>
    /// <example>
    /// <code>
    /// var names = method.AddParameter(TypeDefinition.IEnumerable(typeof(string)), "names");
    /// var loop = method.ForEach("name", names);
    /// loop.AddIndentedStatement(loop.Instance.Invoke("Trim"));
    /// </code>
    /// which is
    /// <code>
    /// foreach(var name in names)
    /// {
    ///     name.Trim();
    /// }
    /// </code>
    /// </example>
    /// <para>
    /// <paramref name="enumerableComponent"/> is a component rather than a string, which is what
    /// lets a <see cref="ParameterDefinition"/> or a <see cref="PropertyDefinition.Instance"/> be
    /// passed directly. Use <see cref="For(string, object, object)"/> instead when the body needs
    /// the index rather than the element.
    /// </para>
    /// </remarks>
    public ForEachDefinition ForEach(string variable, IOutputComponent enumerableComponent)
    {
        return Add(new ForEachDefinition(variable, enumerableComponent));
    }

    /// <summary>
    /// A counting loop, <c>for(var i = 0; i &lt; limit; i++)</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ForDefinition"/> existed but wrote nothing and nothing returned one, so a
    /// <c>for</c> loop had to be hand-written through <see cref="AddCode(string, object[])"/>.
    /// </para>
    /// <example>
    /// <code>
    /// var loop = method.For("i", 0, 10);
    /// loop.AddIndentedStatement("total += i");
    /// </code>
    /// which is
    /// <code>
    /// for(var i = 0; i &lt; 10; i++)
    /// {
    ///     total += i;
    /// }
    /// </code>
    /// </example>
    /// <para>
    /// The limit is exclusive - <c>&lt;</c>, not <c>&lt;=</c> - and takes anything the rest of the
    /// library takes as a value, so it can be an expression such as a collection's <c>Count</c>.
    /// Read the loop variable back off <see cref="ForDefinition.Variable"/> to use it in the body.
    /// Use <see cref="ForEach"/> when the body wants the element rather than the index.
    /// </para>
    /// </remarks>
    public ForDefinition For(string variable, object startValue, object exclusiveLimit)
    {
        return Add(new ForDefinition(variable, startValue, exclusiveLimit));
    }

    /// <summary>
    /// A loop with all three clauses given directly. Any of them may be null.
    /// </summary>
    /// <remarks>
    /// For the loops the counting overload cannot express - counting down, stepping by two, walking
    /// a linked list. All three null is <c>for(; ; )</c>, which is a legal infinite loop.
    /// <see cref="ForDefinition.Variable"/> is null here, because there is no one clause this can
    /// read a loop variable out of.
    /// </remarks>
    public ForDefinition For(
        IOutputComponent? initializer, IOutputComponent? condition, IOutputComponent? increment)
    {
        return Add(new ForDefinition(initializer, condition, increment));
    }

    /// <summary>
    /// <c>if (x &gt; 1) { }</c>. The body is the block this returns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The condition is written inside the parentheses exactly as given, so it must not carry its
    /// own - <c>If("x &gt; 1")</c>, not <c>If("(x &gt; 1)")</c>.
    /// </para>
    /// <example>
    /// <code>
    /// var test = method.If("x &gt; 1");
    /// test.Return("1");
    /// test.ElseIf("x &gt; 0").Return("0");
    /// test.Else().Return("-1");
    /// </code>
    /// which is
    /// <code>
    /// if (x &gt; 1)
    /// {
    ///     return 1;
    /// }
    /// else if (x &gt; 0)
    /// {
    ///     return 0;
    /// }
    /// else
    /// {
    ///     return -1;
    /// }
    /// </code>
    /// </example>
    /// <para>
    /// Note which object each call belongs to: <see cref="IfElseLogicBlockDefinition.ElseIf(string)"/>
    /// and <see cref="IfElseLogicBlockDefinition.Else"/> are on the <c>if</c>, and each returns the
    /// block for its own arm. Calling <c>Else()</c> twice replaces the first one rather than adding
    /// a second.
    /// </para>
    /// </remarks>
    public IfElseLogicBlockDefinition If(string ifStatement)
    {
        return Add(new IfElseLogicBlockDefinition(new CodeOutputComponent(ifStatement) { Indented = false }));
    }

    /// <inheritdoc cref="If(string)" />
    /// <remarks>
    /// The overload for a condition built out of components - <see cref="SyntaxHelpers.And(object[])"/>,
    /// <see cref="SyntaxHelpers.EqualsStatement"/>, <see cref="SyntaxHelpers.Is"/>. A
    /// <see cref="LogicStatement"/> passed here drops the parentheses it would print on its own,
    /// because the <c>if</c> supplies them.
    /// </remarks>
    public IfElseLogicBlockDefinition If(IOutputComponent outputComponent)
    {
        return Add(new IfElseLogicBlockDefinition(outputComponent));
    }

    /// <summary>
    /// The value half of an assignment. Finish it with <see cref="ToClass.To(string)"/>,
    /// <see cref="ToClass.ToVar"/> or <see cref="ToClass.ToLocal"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>The argument is the value, not the destination</strong> - the call reads
    /// right-to-left against the C# it writes. <c>Assign("1").To("x")</c> is <c>x = 1;</c>.
    /// </para>
    /// <example>
    /// <code>
    /// method.Assign("1").To("x");                                  // x = 1;
    /// method.Assign("1").ToVar("x");                               // var x = 1;
    /// method.Assign("1").ToLocal(TypeDefinition.Get(typeof(int)), "x"); // int x = 1;
    /// </code>
    /// </example>
    /// <para>
    /// Nothing is added to the block until the second half is called: an <c>Assign</c> whose result
    /// is discarded writes nothing at all.
    /// </para>
    /// </remarks>
    public ToClass Assign(string value)
    {
        return new ToClass(c => StatementList.Add(c), new CodeOutputComponent(value) { Indented = false });
    }

    /// <inheritdoc cref="Assign(string)" />
    /// <remarks>
    /// The overload for a value built out of components - an invocation, a <c>new</c>, another
    /// instance - so the type it mentions reaches the file as a type.
    /// </remarks>
    public ToClass Assign(IOutputComponent value)
    {
        return new ToClass(c => StatementList.Add(c), value);
    }

    /// <summary>
    /// The half-built assignment <see cref="Assign(string)"/> returns: it holds the value and is
    /// waiting to be told where it goes.
    /// </summary>
    public class ToClass
    {
        private readonly IOutputComponent _valueComponent;
        private readonly Action<IOutputComponent> _addStatement;

        /// <summary>
        /// Built by <see cref="BaseBlockDefinition.Assign(string)"/>; there is no reason to
        /// construct one.
        /// </summary>
        public ToClass(Action<IOutputComponent> addStatement, IOutputComponent valueComponent)
        {
            _addStatement = addStatement;
            _valueComponent = valueComponent;
        }

        /// <summary>
        /// Assigns to an existing destination given as a component - a property, a parameter, an
        /// indexed element.
        /// </summary>
        public void To(IOutputComponent outputComponent)
        {

            _addStatement(new AssignmentStatement(_valueComponent, outputComponent));
        }

        /// <summary>
        /// Assigns to an existing destination named as text: <c>x = 1;</c>.
        /// </summary>
        /// <remarks>
        /// The destination must already exist. Use <see cref="ToVar"/> or <see cref="ToLocal"/> to
        /// declare it at the same time.
        /// </remarks>
        public void To(string destination)
        {
            To(new CodeOutputComponent(destination) { Indented = false });
        }

        /// <summary>
        /// Declares a new local with <c>var</c> and assigns to it: <c>var x = 1;</c>.
        /// </summary>
        /// <remarks>
        /// Returns the local as a value, so the statements after it refer to it by holding it
        /// rather than by repeating its name. Use <see cref="ToLocal"/> where the declared type
        /// matters - a <c>var</c> whose right-hand side is a lambda or a method group does not
        /// compile, and one inferring an anonymous type cannot be named later.
        /// </remarks>
        public InstanceDefinition ToVar(string name)
        {
            var newLocalVariableDefinition = new InstanceDefinition(name){ Indented = false };

            var assignmentStatement = 
                new AssignmentStatement(_valueComponent, newLocalVariableDefinition) { Indented = false };

            _addStatement(new VarStatement(assignmentStatement));

            return newLocalVariableDefinition;
        }

        /// <summary>
        /// Declares a new local with an explicit type and assigns to it: <c>int x = 1;</c>.
        /// </summary>
        /// <remarks>
        /// The type is written as a type, so the file derives whatever <c>using</c> it needs and
        /// qualifies it when the mode qualifies. Returns the local as a value, the same way
        /// <see cref="ToVar"/> does.
        /// </remarks>
        public InstanceDefinition ToLocal(ITypeDefinition typeDefinition, string name)
        {
            var newLocalVariableDefinition = new InstanceDefinition(name) { Indented = false };

            var assignmentStatement =
                new AssignmentStatement(_valueComponent, newLocalVariableDefinition) { Indented = false };

            _addStatement(new DeclarationStatement(typeDefinition, assignmentStatement));

            return newLocalVariableDefinition;
        }
    }

    protected void WriteBlock(IOutputContext context)
    {
        context.OpenScope();

        foreach (var outputComponent in StatementList)
        {
            outputComponent.WriteOutput(context);
        }

        context.CloseScope();
    }
}