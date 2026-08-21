namespace CSharpAuthor;

/// <summary>
/// Where the opening brace of a scope is written.
/// </summary>
/// <remarks>
/// A scope is recorded as a marker rather than as the characters <c>{</c> and <c>}</c>, so this is
/// decided when the file is serialized rather than when it is written. Nothing in the tree changes
/// between the two.
/// </remarks>
public enum BraceStyle
{
    /// <summary>The brace goes on its own line, indented with the construct that opened it.</summary>
    Allman,

    /// <summary>The brace joins the end of the line that opened it.</summary>
    KAndR,
}
