using System.Collections.Generic;
using System.Linq;

namespace CSharpAuthor.Profiles;

/// <summary>
/// A sequence of values: <c>[1, 2, 3]</c> on C# 12, <c>new int[] { 1, 2, 3 }</c> before it.
/// </summary>
/// <remarks>
/// The node says "these values, in this order". Which of the two spellings comes out is the
/// writer's decision, taken from the profile at the moment of writing - which is why the same
/// tree can produce both.
/// </remarks>
public class CollectionExpressionStatement : BaseOutputComponent
{
    private readonly ITypeDefinition? _elementType;
    private readonly IReadOnlyList<IOutputComponent> _values;

    /// <summary>
    /// A collection of values.
    /// </summary>
    /// <param name="elementType">
    /// The element type, used by the array form. Null emits <c>new[] { ... }</c> and leaves the
    /// type to inference - which the compiler can only do when there is at least one value.
    /// </param>
    /// <param name="values">The values.</param>
    public CollectionExpressionStatement(ITypeDefinition? elementType, params IOutputComponent[] values)
        : this(elementType, (IReadOnlyList<IOutputComponent>)values)
    {
    }

    /// <inheritdoc cref="CollectionExpressionStatement(ITypeDefinition,IOutputComponent[])" />
    public CollectionExpressionStatement(ITypeDefinition? elementType, IReadOnlyList<IOutputComponent> values)
    {
        _elementType = elementType;
        _values = values;
    }

    /// <summary>
    /// A collection of values of any kind, converted the way the rest of the library converts
    /// them.
    /// </summary>
    public static CollectionExpressionStatement Of(ITypeDefinition? elementType, params object[] values) =>
        new CollectionExpressionStatement(
            elementType, CodeOutputComponent.GetAll(values).ToList());

    /// <summary>What this collection is for, named in any diagnostic it produces.</summary>
    public string? Context { get; set; }

    /// <inheritdoc />
    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        var session = outputContext.EmitSession();

        if (session.MayEmit(LanguageFeature.CollectionExpressions, outputContext, Context))
        {
            outputContext.Write("[");
            _values.OutputCommaSeparatedList(outputContext);
            outputContext.Write("]");

            return;
        }

        if (_elementType == null && _values.Count == 0)
        {
            // `new[] { }` is CS0826 - an implicitly typed array needs something to infer from.
            // Guessing `object` would compile and be a different collection than the one asked
            // for, so this is a hard stop rather than a substitution.
            session.NoDownlevelAvailable(
                LanguageFeature.CollectionExpressions,
                outputContext,
                Context,
                "an empty collection cannot be written as an implicitly typed array. " +
                "Give the collection its element type.");

            return;
        }

        outputContext.Write("new");

        if (_elementType != null)
        {
            // `new int[]` takes the space; `new[]` does not, and `new []` is not how anyone writes
            // it. Spacing is a property of what is on either side, not of the keyword.
            outputContext.WriteSpace();
            outputContext.Write(_elementType);
        }

        outputContext.Write("[]");

        if (_values.Count == 0)
        {
            outputContext.Write(" { }");

            return;
        }

        outputContext.Write(" { ");
        _values.OutputCommaSeparatedList(outputContext);
        outputContext.Write(" }");
    }
}
