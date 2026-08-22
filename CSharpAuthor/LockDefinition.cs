namespace CSharpAuthor;

/// <summary>
/// A <c>lock</c> statement. Built by <see cref="BaseBlockDefinition.Lock(IOutputComponent)"/>.
/// </summary>
/// <remarks>
/// There was no route to a <c>lock</c> before this: <c>AddCode</c> writes a line and does not open
/// a scope, so the braces had to be written by hand and the body indented by hand with them. Any
/// generator emitting a cache, a lazy singleton or a double-checked initialisation needs one.
/// </remarks>
public class LockDefinition : BaseBlockDefinition
{
    private readonly IOutputComponent _lockObject;

    /// <summary>
    /// A <c>lock</c> on <paramref name="lockObject"/>. Prefer
    /// <see cref="BaseBlockDefinition.Lock(IOutputComponent)"/>, which builds one and attaches it
    /// to a block.
    /// </summary>
    public LockDefinition(IOutputComponent lockObject)
    {
        _lockObject = lockObject;
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.WriteIndent("lock (");
        _lockObject.WriteOutput(outputContext);
        outputContext.WriteLine(")");

        WriteBlock(outputContext);
    }
}

/// <summary>
/// A block with a caller-supplied header - <c>unsafe</c>, <c>checked</c>, <c>fixed (...)</c>, or
/// anything else this library has no dedicated node for.
/// </summary>
/// <remarks>
/// The escape hatch for control flow, and the counterpart to <c>AddCode</c>'s escape hatch for
/// statements. <c>AddCode</c> cannot stand in for one: it writes a line and returns, so it never
/// opens a scope, and a hand-written <c>{</c> leaves the body at the wrong indent and the closing
/// brace to the caller. This opens a real scope, so the body indents and closes itself.
/// </remarks>
public class BlockDefinition : BaseBlockDefinition
{
    private readonly string _header;

    /// <summary>
    /// A block introduced by <paramref name="header"/>, written as given.
    /// </summary>
    public BlockDefinition(string header)
    {
        _header = header ?? "";
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.WriteIndentedLine(_header);

        WriteBlock(outputContext);
    }
}
