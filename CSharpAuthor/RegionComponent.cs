using System.Collections.Generic;

namespace CSharpAuthor;

/// <summary>
/// A <c>#region</c> and its <c>#endregion</c>, with whatever they enclose.
/// </summary>
/// <remarks>
/// <para>
/// A container rather than a pair of markers, so the <c>#endregion</c> cannot go missing or land in
/// the wrong place. An unbalanced region is CS1038 at the end of the file, which points at the last
/// line rather than at the one that opened it.
/// </para>
/// <para>
/// The region adds no scope, so what it holds is written at the indentation it would have had
/// anyway. Directives are indented with the code around them, as
/// <see cref="PragmaOutputComponent"/> and <see cref="NullableEnableComponent"/> already are.
/// </para>
/// </remarks>
public class RegionComponent : BaseOutputComponent
{
    private readonly List<IOutputComponent> _components = new();

    /// <param name="name">
    /// The region's label. Free text - it runs to the end of the line, so it needs no quoting and
    /// may contain spaces.
    /// </param>
    public RegionComponent(string name = "")
    {
        Name = name;
    }

    /// <summary>The label written after <c>#region</c>.</summary>
    public string Name { get; }

    /// <summary>Puts a component inside the region. Returns it, typed, so it stays usable.</summary>
    public T Add<T>(T component) where T : IOutputComponent
    {
        _components.Add(component);

        return component;
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.WriteIndentedLine(
            string.IsNullOrEmpty(Name) ? "#region" : "#region " + Name);

        foreach (var component in _components)
        {
            component.WriteOutput(outputContext);
        }

        outputContext.WriteIndentedLine("#endregion");
    }

    /// <remarks>
    /// A directive takes no attributes and no documentation comment; either would be written
    /// above the <c>#region</c> line and belong to nothing.
    /// </remarks>
    protected override void ProcessLeadingTraits(IOutputContext outputContext)
    {
    }

    /// <inheritdoc cref="ProcessLeadingTraits"/>
    protected override void ProcessTrailingTraits(IOutputContext outputContext)
    {
    }
}
