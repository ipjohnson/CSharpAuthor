namespace CSharpAuthor;

/// <summary>
/// Where an opening brace goes.
/// </summary>
/// <remarks>
/// The same two values, with the same names, as the style options in
/// <c>proto/deferred/DeferredContext.cs</c> - that context can honour both because it decides
/// layout at <c>Output()</c>, when the whole file is known. <see cref="OutputContext"/> writes
/// characters as it goes and can only produce <see cref="Allman"/>; a profile asking for
/// <see cref="KAndR"/> through it is carried but not applied.
/// </remarks>
public enum BraceStyle
{
    /// <summary>The brace is on its own line.</summary>
    Allman,

    /// <summary>The brace ends the line that opened the construct.</summary>
    KAndR
}
