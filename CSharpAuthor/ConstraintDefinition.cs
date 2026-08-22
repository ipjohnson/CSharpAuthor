using System;
using System.Collections.Generic;

namespace CSharpAuthor;

/// <summary>
/// The constraints on one type parameter, written as <c>where T : class, IComparable&lt;T&gt;, new()</c>.
/// </summary>
/// <remarks>
/// <para>
/// A <c>where</c> clause could always be written by assigning a <c>CodeOutputComponent</c> to
/// <see cref="ClassDefinition.WhereStatement"/>, and for a clause a generator already holds as text
/// that is still the shortest route. Building one part by part is where the string gets fiddly,
/// because C# fixes the order and the compiler is the only thing that checks it:
/// </para>
/// <list type="bullet">
/// <item>the primary constraint, if any, comes first and there is at most one</item>
/// <item>interfaces and base types come next</item>
/// <item><c>new()</c> comes last, and cannot be combined with <c>struct</c> or <c>unmanaged</c>,
/// which already imply it</item>
/// </list>
/// <para>
/// Assembling that by hand puts the ordering rules in every caller. This holds the parts and writes
/// them in the order C# requires, so a caller adds what it read from a symbol in whatever order it
/// read them.
/// </para>
/// <example>
/// <code>
/// classDefinition.AddConstraint("T").Class().Implements(comparable).DefaultConstructor();
/// // where T : class, IComparable&lt;T&gt;, new()
/// </code>
/// </example>
/// </remarks>
public class ConstraintDefinition
{
    private readonly List<ITypeDefinition> _types = new();
    private string? _primary;
    private bool _defaultConstructor;

    /// <summary>
    /// Constraints for the parameter named <paramref name="typeParameter"/>. Prefer
    /// <see cref="ClassDefinition.AddConstraint"/> or <see cref="MethodDefinition.AddConstraint"/>,
    /// which build one, attach it, and return the existing one if the parameter is already
    /// constrained - a second <c>where</c> for one parameter does not compile.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="typeParameter"/> is null or
    /// blank.</exception>
    public ConstraintDefinition(string typeParameter)
    {
        if (string.IsNullOrWhiteSpace(typeParameter))
        {
            throw new ArgumentException("A constraint has to name the type parameter it constrains.",
                nameof(typeParameter));
        }

        TypeParameter = typeParameter;
    }

    /// <summary>
    /// The parameter being constrained, which is the name after <c>where</c>.
    /// </summary>
    public string TypeParameter { get; }

    /// <summary>
    /// Whether anything has been added. An empty constraint writes nothing rather than a
    /// <c>where T :</c> with nothing after it.
    /// </summary>
    public bool IsEmpty => _primary == null && _types.Count == 0 && !_defaultConstructor;

    /// <summary>
    /// <c>where T : class</c>, or <c>class?</c> when <paramref name="nullable"/>.
    /// </summary>
    public ConstraintDefinition Class(bool nullable = false) => Primary(nullable ? "class?" : "class");

    /// <summary>
    /// <c>where T : struct</c>. Implies a default constructor, so <see cref="DefaultConstructor"/>
    /// cannot be added as well.
    /// </summary>
    public ConstraintDefinition Struct() => Primary("struct");

    /// <summary>
    /// <c>where T : unmanaged</c>. Narrower than <see cref="Struct"/>, and likewise implies a
    /// default constructor.
    /// </summary>
    public ConstraintDefinition Unmanaged() => Primary("unmanaged");

    /// <summary>
    /// <c>where T : notnull</c>.
    /// </summary>
    public ConstraintDefinition NotNull() => Primary("notnull");

    /// <summary>
    /// <c>where T : default</c>, which is how an override releases a constraint it inherited.
    /// </summary>
    public ConstraintDefinition Default() => Primary("default");

    /// <summary>
    /// A base class or interface the argument has to derive from or implement. Several may be added,
    /// and they are written in the order they were added.
    /// </summary>
    /// <remarks>
    /// A base class and an interface occupy the same position in the clause and C# does not
    /// distinguish them there, so one method covers both. A base class still has to be written before
    /// any interface, which is the caller's ordering to keep.
    /// </remarks>
    public ConstraintDefinition Implements(ITypeDefinition type)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        _types.Add(type);

        return this;
    }

    /// <summary>
    /// <c>new()</c>, written last.
    /// </summary>
    public ConstraintDefinition DefaultConstructor()
    {
        if (_primary is "struct" or "unmanaged")
        {
            throw new InvalidOperationException(
                $"'{TypeParameter}' is constrained to '{_primary}', which already guarantees a " +
                "default constructor, so new() cannot be added as well.");
        }

        _defaultConstructor = true;

        return this;
    }

    /// <summary>
    /// Writes <c>where T : ...</c>, with no leading or trailing space.
    /// </summary>
    /// <summary>
    /// <see cref="_types"/> with any non-interface constraint moved to the front, preserving the
    /// relative order of everything else.
    /// </summary>
    private IEnumerable<ITypeDefinition> OrderedTypes()
    {
        var hasBaseType = false;

        foreach (var type in _types)
        {
            if (type.TypeDefinitionEnum != TypeDefinitionEnum.InterfaceDefinition)
            {
                hasBaseType = true;
                break;
            }
        }

        if (!hasBaseType)
        {
            return _types;
        }

        var ordered = new List<ITypeDefinition>(_types.Count);

        foreach (var type in _types)
        {
            if (type.TypeDefinitionEnum != TypeDefinitionEnum.InterfaceDefinition)
            {
                ordered.Add(type);
            }
        }

        foreach (var type in _types)
        {
            if (type.TypeDefinitionEnum == TypeDefinitionEnum.InterfaceDefinition)
            {
                ordered.Add(type);
            }
        }

        return ordered;
    }

    public void WriteOutput(IOutputContext outputContext)
    {
        if (IsEmpty)
        {
            return;
        }

        outputContext.Write("where ");
        outputContext.Write(TypeParameter);
        outputContext.Write(" : ");

        var written = false;

        if (_primary != null)
        {
            outputContext.Write(_primary);
            written = true;
        }

        // C# requires the base-class constraint first: `where T : Stream, IDisposable` compiles
        // and `where T : IDisposable, Stream` is CS0406. Callers add them in whatever order they
        // read them - a loop over a symbol's ConstraintTypes has no reason to sort - and the type
        // model already knows which is which, so order them here rather than make it the caller's
        // problem. At most one is not an interface, so this moves at most one entry.
        foreach (var type in OrderedTypes())
        {
            if (written)
            {
                outputContext.Write(", ");
            }

            outputContext.Write(type);
            written = true;
        }

        if (_defaultConstructor)
        {
            if (written)
            {
                outputContext.Write(", ");
            }

            outputContext.Write("new()");
        }
    }

    /// <summary>
    /// The one primary constraint, which has to come first and cannot be joined by another.
    /// </summary>
    private ConstraintDefinition Primary(string keyword)
    {
        if (_primary != null && _primary != keyword)
        {
            throw new InvalidOperationException(
                $"'{TypeParameter}' is already constrained to '{_primary}', and '{keyword}' is also a " +
                "primary constraint. A type parameter may have only one.");
        }

        if (_defaultConstructor && keyword is "struct" or "unmanaged")
        {
            throw new InvalidOperationException(
                $"'{TypeParameter}' already has a new() constraint, which '{keyword}' implies. " +
                "The two cannot be combined.");
        }

        _primary = keyword;

        return this;
    }
}
