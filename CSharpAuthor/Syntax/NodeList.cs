#nullable enable

using System.Collections.Generic;

namespace CSharpAuthor.Syntax;

/// <summary>
/// A list of grammar nodes, plus the one thing a bare <see cref="List{T}"/> has nowhere to put:
/// whether the list ended with a separator.
/// </summary>
/// <remarks>
/// <para>
/// <c>{ 1, 2, }</c> and <c>{ 1, 2 }</c> are both legal C#, and they are not the same tree - Roslyn
/// keeps the extra <c>,</c> as a token of the separated list, so anything comparing an emitted tree
/// against its source sees a token that went missing. A writer that joins elements with an
/// <c>if (i &gt; 0)</c> separator can only ever produce the second, whatever the first said.
/// </para>
/// <para>
/// The flag is on the list rather than on each node that owns one because it is a property of the
/// list: every separated list in the grammar can carry it, and none of them needs a field of its
/// own to say so. It is set only for a <c>SeparatedSyntaxList</c>; an unseparated list has no
/// separator to trail, and <see cref="SyntaxWriter.List{T}"/> ignores it for those styles rather
/// than inventing one.
/// </para>
/// </remarks>
#if CSHARPAUTHOR_PUBLIC_SYNTAX
public
#endif
sealed class NodeList<T> : List<T> where T : ISyntax
{
    /// <summary>
    /// True when a separator follows the last element - the trailing comma of <c>{ 1, 2, }</c>.
    /// Ignored when the list has no elements, and ignored for a list style that has no separator.
    /// </summary>
    public bool TrailingSeparator { get; set; }
}
