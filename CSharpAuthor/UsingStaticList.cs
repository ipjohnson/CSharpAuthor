using System;
using System.Collections.Generic;

namespace CSharpAuthor;

/// <summary>
/// The <c>using static Target;</c> directives a caller asked one file or namespace declaration for,
/// in the order they were added.
/// </summary>
internal sealed class UsingStaticList
{
    private readonly List<ITypeDefinition> _targets = new();

    public IReadOnlyList<ITypeDefinition> Targets => _targets;

    public void Add(ITypeDefinition target)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        foreach (var existing in _targets)
        {
            if (UsingAliasList.SameType(existing, target))
            {
                return;
            }
        }

        _targets.Add(target);
    }
}
