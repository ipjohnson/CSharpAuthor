using System.Collections.Generic;

namespace CSharpAuthor;

/// <summary>
/// A type in a base list, and the arguments its constructor is called with.
/// </summary>
/// <remarks>
/// The arguments are what makes <c>record Dog(string Id) : Pet(Id)</c> expressible. A base list
/// entry with none of them writes exactly as it did when the list held bare types, so an interface
/// - which can never take arguments - is unaffected.
/// </remarks>
internal readonly struct BaseTypeReference
{
    private readonly IReadOnlyList<IOutputComponent> _arguments;

    public BaseTypeReference(ITypeDefinition type, IReadOnlyList<IOutputComponent> arguments)
    {
        Type = type;
        _arguments = arguments;
    }

    public ITypeDefinition Type { get; }

    /// <summary>How many arguments this entry carries, so a repeat call can be told from a fuller one.</summary>
    public int ArgumentCount => _arguments?.Count ?? 0;

    public void WriteOutput(IOutputContext outputContext)
    {
        outputContext.Write(Type);

        if (_arguments == null || _arguments.Count == 0)
        {
            return;
        }

        outputContext.Write("(");

        _arguments.OutputCommaSeparatedList(outputContext);

        outputContext.Write(")");
    }
}
