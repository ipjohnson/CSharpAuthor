using System;

namespace CSharpAuthor;

/// <summary>Which of C#'s two conversion operators this is.</summary>
public enum ConversionOperatorKind
{
    /// <summary><c>implicit</c> - the compiler applies the conversion without being asked.</summary>
    Implicit,

    /// <summary><c>explicit</c> - the conversion has to be written as a cast.</summary>
    Explicit
}

/// <summary>
/// A user-defined conversion: <c>public static implicit operator Shape(Circle value)</c>.
/// </summary>
/// <remarks>
/// <para>
/// Prefer <see cref="ClassDefinition.AddConversionOperator(ConversionOperatorKind,ITypeDefinition,ITypeDefinition,string)"/>,
/// which builds one and attaches it with its parameter already in place.
/// </para>
/// <para>
/// <strong>Why this exists as a type rather than a naming convention.</strong> A conversion
/// operator was reachable before only by naming a method after it -
/// <c>AddMethod("operator " + target)</c> with <c>SetReturnType(TypeDefinition.Get("", "implicit"))</c>
/// to get the keyword into the slot where a return type goes. That emits the right characters, and
/// it is a latent correctness bug: the target type is a string by then, so
/// <see cref="IOutputContext.Write(ITypeDefinition)"/> never sees it. No <c>global::</c>
/// qualification, no derived <c>using</c>. It works while the target is the enclosing type and
/// silently emits CS0246 the first time it is not.
/// </para>
/// <para>
/// Here the target is an <see cref="ITypeDefinition"/> all the way to the writer, so it is tracked
/// like every other type in the file and one option still flips the whole file between short names
/// and <c>global::</c>.
/// </para>
/// <example>
/// <code>
/// var shape = file.AddClass("Shape");
/// var conversion = shape.AddConversionOperator(
///     ConversionOperatorKind.Implicit, shapeType, circleType, "value");
///
/// conversion.Return(Ex.New(shapeType, "value"));
/// </code>
/// which is <c>public static implicit operator Shape(Circle value) { return new Shape(value); }</c>.
/// </example>
/// </remarks>
public class ConversionOperatorDefinition : MethodDefinition
{
    /// <summary>
    /// A conversion to <paramref name="targetType"/>. The parameter is the source type and is added
    /// like any other.
    /// </summary>
    public ConversionOperatorDefinition(ConversionOperatorKind kind, ITypeDefinition targetType)
        : base("operator " + (targetType ?? throw new ArgumentNullException(nameof(targetType))).Name)
    {
        Kind = kind;
        TargetType = targetType;

        // C# requires both, and there is no conversion operator that is anything else. Defaulting
        // them means a caller cannot produce CS0558 by forgetting; assigning Modifiers still wins
        // for anyone who has a reason to.
        Modifiers = ComponentModifier.Public | ComponentModifier.Static;
    }

    /// <summary>Implicit or explicit.</summary>
    public ConversionOperatorKind Kind { get; }

    /// <summary>The type converted to, left unrendered so the file spells it.</summary>
    public ITypeDefinition TargetType { get; }

    /// <summary>
    /// <c>public static implicit operator Target(Source value)</c>.
    /// </summary>
    /// <remarks>
    /// The whole signature is written here rather than through the base, because a conversion
    /// operator has no name and its target type stands where a return type would - so neither
    /// <see cref="MethodDefinition.WriteReturnType"/> nor the name slot describes it.
    /// </remarks>
    protected override void WriteMethodSignature(IOutputContext outputContext)
    {
        WriteAccessModifier(outputContext);

        outputContext.Write(Kind == ConversionOperatorKind.Implicit ? "implicit" : "explicit");
        outputContext.WriteSpace();
        outputContext.Write("operator");
        outputContext.WriteSpace();

        // The point of the whole type: the target reaches the writer as a definition, so it is
        // tracked, qualified and imported like every other type the file names.
        outputContext.Write(TargetType);

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
}
