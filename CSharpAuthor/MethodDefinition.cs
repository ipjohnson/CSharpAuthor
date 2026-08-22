using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CSharpAuthor;

/// <summary>
/// A method: a signature, and a block of statements for its body.
/// </summary>
/// <remarks>
/// <para>
/// It is a <see cref="BaseBlockDefinition"/>, so the body is built with the same statement methods
/// a loop or an <c>if</c> body uses - <c>AddCode</c>, <c>AddIndentedStatement</c>, <c>Assign</c>,
/// <c>If</c>, <c>ForEach</c>, <c>Return</c>.
/// </para>
/// <example>
/// <code>
/// var greet = greeter.AddMethod("Greet");
/// greet.SetReturnType(typeof(string));
/// greet.AddParameter(typeof(string), "name");
/// greet.Return("name");
/// </code>
/// which is
/// <code>
/// public string Greet(string name)
/// {
///     return name;
/// }
/// </code>
/// </example>
/// <para>
/// A method with nothing set is <c>public void</c> with an empty body. Both defaults are chosen so
/// the common case needs no call: accessibility comes from
/// <see cref="BaseOutputComponent.Modifiers"/>, and no return type means <c>void</c> rather than
/// an error.
/// </para>
/// </remarks>
public class MethodDefinition : BaseBlockDefinition, INamedComponent
{
    protected readonly List<ParameterDefinition> ParameterList = new ();
    private readonly List<ITypeDefinition> _genericParameters = new();
    private readonly List<ConstraintDefinition> _constraints = new();
    protected int VariableCount = 1;

    private ITypeDefinition? _returnType;

    /// <summary>
    /// A method named <paramref name="name"/>. Prefer
    /// <see cref="ClassDefinition.AddMethod"/>, which builds one and attaches it to a type.
    /// </summary>
    public MethodDefinition(string name)
    {
        Name = name;
    }

    /// <summary>The declared name, escaped with <c>@</c> if it is a keyword.</summary>
    public string Name { get; }

    /// <summary>
    /// The body of the <c>&lt;returns&gt;</c> element in the method's documentation comment.
    /// </summary>
    /// <remarks>
    /// Written only when <see cref="BaseOutputComponent.Comment"/> is also set, because
    /// <c>&lt;returns&gt;</c> with no <c>&lt;summary&gt;</c> above it is a documentation comment
    /// with nothing in it. Parameters are documented the same way, through
    /// <see cref="BaseOutputComponent.Comment"/> on each <see cref="ParameterDefinition"/>.
    /// </remarks>
    public string? ReturnComment { get; set; }

    /// <summary>
    /// The method's own type parameters, written as <c>Name&lt;T, U&gt;</c>.
    /// </summary>
    /// <remarks>
    /// The type's parameters are a different list, on <see cref="ClassDefinition.GenericParameters"/>;
    /// a method does not repeat them.
    /// </remarks>
    public List<ITypeDefinition> GenericParameters => _genericParameters;

    /// <summary>
    /// The constraint clause, written after the parameter list: <c>where T : new()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A clause the caller has already rendered. <see cref="AddConstraint"/> builds one part by part
    /// instead, and the two can be used together — this is written first.
    /// </para>
    /// <para>
    /// Written verbatim into the signature, leading space included, so it has to start with one:
    /// <c>new CodeOutputComponent(" where T : new()") { Indented = false }</c>. That is the reason
    /// to prefer <see cref="AddConstraint"/> - it writes the spacing, orders the parts the way C#
    /// requires, and merges two calls for the same parameter instead of emitting a second
    /// <c>where</c> for it, which would not compile.
    /// </para>
    /// </remarks>
    public IOutputComponent? WhereStatement { get; set; }

    /// <summary>
    /// The constraints declared through <see cref="AddConstraint"/>.
    /// </summary>
    public IReadOnlyList<ConstraintDefinition> Constraints => _constraints;

    /// <summary>
    /// Constrains one of this method's type parameters, written after the parameter list.
    /// </summary>
    /// <remarks>
    /// Returns the constraint so its parts can be added in any order; they are written in the order
    /// C# requires. Calling this twice for one parameter returns the same constraint rather than
    /// declaring a second <c>where</c> for it, which would not compile.
    /// </remarks>
    public ConstraintDefinition AddConstraint(string typeParameter)
    {
        foreach (var existing in _constraints)
        {
            if (existing.TypeParameter == typeParameter)
            {
                return existing;
            }
        }

        var constraint = new ConstraintDefinition(typeParameter);

        _constraints.Add(constraint);

        return constraint;
    }
    
    /// <summary>
    /// The interface this method explicitly implements: <c>string IGreeter.Greet()</c>.
    /// </summary>
    /// <remarks>
    /// An explicit implementation takes no accessibility keyword and none of the inheritance
    /// modifiers - the interface decides all of that - so those are dropped when this is set.
    /// <see cref="ComponentModifier.Async"/> survives, because it is not one of them.
    /// </remarks>
    public ITypeDefinition? InterfaceImplementation { get; set; }

    /// <summary>
    /// The declared return type, or null for <c>void</c>.
    /// </summary>
    public ITypeDefinition? ReturnType => _returnType;

    /// <summary>
    /// The parameters, in the order they will be written.
    /// </summary>
    /// <remarks>
    /// A <see cref="ParameterDefinition"/> is also usable as a value, so the body can refer to a
    /// parameter by holding it rather than by repeating its name - and
    /// <see cref="ParameterDefinition.AsArgument"/> forwards it to another call with its
    /// <c>ref</c> or <c>out</c> intact.
    /// </remarks>
    public IReadOnlyList<ParameterDefinition> Parameters => ParameterList;

    /// <summary>
    /// A local variable name this method has not used yet - <c>item1</c>, <c>item2</c>, and so on.
    /// </summary>
    /// <remarks>
    /// For generated code that needs a temporary and has no name for it. The counter is per method,
    /// so it is only unique against other names from this same call: a name a caller wrote by hand
    /// can still collide with it.
    /// </remarks>
    public string GetUniqueVariable(string prefix)
    {
        return prefix + (VariableCount++).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A type parameter on this method: <c>public T Create&lt;T&gt;()</c>.
    /// </summary>
    /// <remarks>
    /// Usually <c>new TypeParameterDefinition("T")</c>, and the same instance - or another built
    /// from the same name - is what the parameters and the return type refer to. Constrain it with
    /// <see cref="AddConstraint"/>.
    /// </remarks>
    public void AddGenericParameter(ITypeDefinition typeDefinition)
    {
        _genericParameters.Add(typeDefinition);
    }

    /// <inheritdoc cref="SetReturnType(ITypeDefinition)" />
    /// <remarks>
    /// The overload for a type this generator can name at compile time. For an async method the
    /// return type is the <c>Task</c>, not the value: <c>SetReturnType(TypeDefinition.Task(typeof(int)))</c>
    /// with <see cref="ComponentModifier.Async"/> set.
    /// </remarks>
    public MethodDefinition SetReturnType(Type type)
    {
        return SetReturnType(TypeDefinition.Get(type));
    }

    /// <summary>
    /// The method's return type. Left unset, the method returns <c>void</c>.
    /// </summary>
    /// <remarks>
    /// Returns the method, so it chains with the other setters. There is no way to un-set it back
    /// to <c>void</c>; a method that should return nothing is one that was never given a type.
    /// </remarks>
    public MethodDefinition SetReturnType(ITypeDefinition type)
    {
        _returnType = type;

        return this;
    }

    /// <inheritdoc cref="AddParameter(ITypeDefinition, string)" />
    /// <remarks>
    /// The overload for a type this generator can name at compile time - <c>typeof(string)</c>.
    /// Reach for <see cref="AddParameter(ITypeDefinition, string)"/> for a type that does not exist
    /// as a <see cref="Type"/>: one this generator is also emitting, one read from a Roslyn symbol,
    /// or one that needed <see cref="ITypeDefinition.MakeNullable"/> or
    /// <see cref="ITypeDefinition.MakeArray()"/> applied to it.
    /// </remarks>
    public ParameterDefinition AddParameter(Type type, string name)
    {
        return AddParameter(TypeDefinition.Get(type), name);
    }

    /// <summary>
    /// A parameter, appended to the signature: <c>public void Greet(string name)</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns the parameter, which is both the declaration and a value the body can use:
    /// </para>
    /// <example>
    /// <code>
    /// var names = run.AddParameter(TypeDefinition.IEnumerable(typeof(string)), "names");
    /// var loop = run.ForEach("name", names);
    /// </code>
    /// which is <c>foreach(var name in names)</c> - the parameter written as an expression, with no
    /// second copy of its name to keep in step.
    /// </example>
    /// <para>
    /// The returned <see cref="ParameterDefinition"/> is where <c>ref</c>, <c>out</c>, <c>in</c>,
    /// <c>params</c>, <c>this</c>, a default value, and the parameter's <c>&lt;param&gt;</c> comment
    /// are set. Parameters are written in the order they are added, so an optional one added before
    /// a required one produces CS1737 in the generated file.
    /// </para>
    /// </remarks>
    public ParameterDefinition AddParameter(ITypeDefinition typeDefinition, string name)
    {
        var parameter = new ParameterDefinition(typeDefinition, name);

        ParameterList.Add(parameter);

        return parameter;
    }

    /// <summary>
    /// Appends a parameter that was built elsewhere, returning the <em>method</em> rather than the
    /// parameter so calls chain.
    /// </summary>
    /// <remarks>
    /// For forwarding: the parameters of a method being wrapped can be added to the wrapper
    /// directly, without rebuilding each one and risking a difference. Note the return type - this
    /// is the overload that does not give you the parameter back, because you already have it.
    /// </remarks>
    public MethodDefinition AddParameter(ParameterDefinition parameterDefinition)
    {
        ParameterList.Add(parameterDefinition);

        return this;
    }
    
    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        WriteMethodSignature(outputContext);

        WriteMethodBody(outputContext);
    }
    
    protected override void WriteComment(IOutputContext outputContext)
    {
        if (string.IsNullOrEmpty(Comment))
        {
            return;
        }
        
        DocumentationComment.WriteSummary(outputContext.WriteIndentedLine, Comment);

        foreach (ParameterDefinition parameterDefinition in ParameterList)
        {
            if (parameterDefinition.Comment != null)
            {
                DocumentationComment.WriteElement(
                    outputContext.WriteIndentedLine,
                    "<param name=\"" + parameterDefinition.Name + "\">",
                    "</param>",
                    parameterDefinition.Comment);
            }
        }

        if (ReturnComment != null)
        {
            DocumentationComment.WriteElement(
                outputContext.WriteIndentedLine, "<returns>", "</returns>", ReturnComment);
        }
    }

    /// <summary>
    /// Declares the method without a body, terminating it with <c>;</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The defining half of a <c>partial</c> method, or an <c>extern</c> one. <c>abstract</c>
    /// implies it, so that does not need setting as well.
    /// </para>
    /// <example>
    /// <code>
    /// greeter.Modifiers = ComponentModifier.Partial;
    /// var configure = greeter.AddMethod("Configure");
    /// configure.Modifiers = ComponentModifier.Partial;
    /// configure.OmitBody = true;
    /// </code>
    /// which is <c>public partial void Configure();</c> - the declaration a source generator
    /// implements from the other side.
    /// </example>
    /// <para>
    /// Statements added to a method with this set are dropped, not written, so it is a statement
    /// about the declaration rather than a way to comment a body out. On an
    /// <see cref="InterfaceMethodDefinition"/> do not set it at all: statements alone decide
    /// whether an interface member has a body there, and this suppresses the body without
    /// restoring the <c>;</c>.
    /// </para>
    /// </remarks>
    public bool OmitBody { get; set; }

    /// <summary>
    /// Whether this declaration ends at the signature.
    /// </summary>
    /// <remarks>
    /// An abstract method used to be written with <c>{ }</c> after it, which is CS0500 - "cannot
    /// declare a body because it is marked abstract". The modifier was being dropped at the same
    /// time, so the result compiled as an ordinary empty method and the abstraction quietly
    /// disappeared instead.
    /// </remarks>
    /// <remarks>
    /// A <c>partial</c> method with no statements is NOT bodyless here, though the defining half of
    /// one has to be: <c>ModifierTests.ModifierMatrixTests.APartialMethodStillWritesItsBodyByDefault</c>
    /// pins the opposite, so <see cref="OmitBody"/> stays the way a caller says which half this is.
    /// That leaves adversary #50 open. See docs/migration-v1-v2.md.
    /// </remarks>
    private bool IsBodyless =>
        OmitBody || (Modifiers & ComponentModifier.Abstract) == ComponentModifier.Abstract;

    protected virtual void WriteMethodBody(IOutputContext outputContext)
    {
        if (IsBodyless)
        {
            return;
        }

        WriteBlock(outputContext);
    }

    protected virtual void WriteMethodSignature(IOutputContext outputContext)
    {
        WriteAccessModifier(outputContext);

        WriteReturnType(outputContext);

        outputContext.WriteSpace();

        if (InterfaceImplementation != null)
        {
            outputContext.Write(InterfaceImplementation);
            outputContext.Write(".");
        }

        outputContext.Write(CSharpIdentifier.Escape(Name));

        if (_genericParameters.Count > 0)
        {
            outputContext.Write("<");
            _genericParameters.OutputCommaSeparatedList(outputContext);
            outputContext.Write(">");
        }
            
        outputContext.Write("(");

        for (var i = 0; i < ParameterList.Count; i++)
        {
            if (i > 0)
            {
                outputContext.Write(", ");
            }

            ParameterList[i].WriteWithSignature(outputContext);
        }

        outputContext.Write(")");

        WriteEndOfMethodSignature(outputContext);
    }

    /// <summary>
    /// The <c>where</c> clauses, written between the parameter list and whatever terminates the
    /// signature.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="WriteEndOfMethodSignature"/> because an interface method terminates
    /// its signature differently and so overrides that method. Before this was pulled out, the
    /// override replaced the constraint loop as well, and every constraint on an interface method
    /// was silently dropped - <c>AddConstraint</c> accepted them and nothing wrote them.
    /// </remarks>
    protected void WriteConstraints(IOutputContext outputContext)
    {
        WhereStatement?.WriteOutput(outputContext);

        foreach (var constraint in _constraints)
        {
            if (constraint.IsEmpty)
            {
                continue;
            }

            outputContext.WriteSpace();

            constraint.WriteOutput(outputContext);
        }
    }

    protected virtual void WriteEndOfMethodSignature(IOutputContext outputContext)
    {
        WriteConstraints(outputContext);

        if (IsBodyless)
        {
            outputContext.Write(";");
        }

        outputContext.WriteLine();
    }

    protected virtual void WriteAccessModifier(IOutputContext outputContext)
    {
        outputContext.WriteIndent();

        // An explicit interface implementation takes no accessibility and none of the inheritance
        // modifiers - the interface decides all of that - but it can still be async.
        if (InterfaceImplementation != null)
        {
            outputContext.Write(
                Modifiers.GetModifierKeywords(ComponentModifier.Async));

            return;
        }

        outputContext.Write(GetAccessModifier(KeyWords.Public));
        outputContext.WriteSpace();

        outputContext.Write(
            Modifiers.GetModifierKeywords(ComponentModifierExtensions.MethodModifiers));
    }

    protected virtual void WriteReturnType(IOutputContext outputContext)
    {
        if (_returnType != null)
        {
            outputContext.Write(_returnType);
        }
        else
        {
            outputContext.Write("void");
        }
    }
}