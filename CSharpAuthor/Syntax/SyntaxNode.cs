#nullable enable

namespace CSharpAuthor.Syntax;

/// <summary>
/// The base of every generated grammar node.
/// </summary>
/// <remarks>
/// <para>
/// It exists for one reason: <see cref="IOutputComponent.AddUsingNamespace"/> is a no-op for
/// every node in the grammar, and 250 copies of an empty method is 750 lines of source that
/// every consumer of the source package would compile. Inheriting it costs one line per node
/// instead of four.
/// </para>
/// <para>
/// A node never adds a namespace itself. Invariant 1: a type reaches output only through
/// <see cref="IOutputContext.Write(ITypeDefinition)"/>, and namespaces are derived from what
/// was written rather than declared alongside it. That is what makes a missing <c>using</c>
/// structurally impossible, so the empty body here is the correct body, not a stub.
/// </para>
/// </remarks>
#if !CSHARPAUTHOR_SOURCE
public
#endif
abstract class SyntaxNode : ISyntax
{
    /// <inheritdoc />
    /// <remarks>Deliberately empty - see the type remarks.</remarks>
    public void AddUsingNamespace(string ns) { }

    /// <inheritdoc />
    public abstract void WriteOutput(IOutputContext outputContext);
}
