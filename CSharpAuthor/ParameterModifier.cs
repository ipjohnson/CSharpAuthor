namespace CSharpAuthor;

/// <summary>
/// How a parameter is passed.
/// </summary>
/// <remarks>
/// The values line up with Roslyn's RefKind, so a parameter read off a symbol can be reproduced
/// without the caller mapping between two vocabularies.
/// </remarks>
public enum ParameterModifier
{
    /// <summary>Passed by value.</summary>
    None,

    /// <summary>Passed by reference: <c>ref int value</c>.</summary>
    Ref,

    /// <summary>Assigned by the callee: <c>out int value</c>.</summary>
    Out,

    /// <summary>Passed by readonly reference: <c>in int value</c>.</summary>
    In,

    /// <summary>Passed by readonly reference, required at the call site: <c>ref readonly int value</c>.</summary>
    RefReadOnly
}
