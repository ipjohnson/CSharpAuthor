using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// The <c>using Name = Target;</c> directives a caller asked one file or namespace declaration for,
/// in the order they were added.
/// </summary>
internal sealed class UsingAliasList
{
    private readonly List<KeyValuePair<string, ITypeDefinition>> _aliases = new();

    /// <summary>Each alias name, without any <c>@</c>, and the type it names.</summary>
    public IReadOnlyList<KeyValuePair<string, ITypeDefinition>> Aliases => _aliases;

    public void Add(string name, ITypeDefinition target)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        var bare = name != null && name.Length > 1 && name[0] == '@' ? name.Substring(1) : name;

        if (!IsIdentifier(bare))
        {
            throw new ArgumentException(
                "A using alias needs a single identifier for its name, such as Json.", nameof(name));
        }

        foreach (var existing in _aliases)
        {
            if (!string.Equals(existing.Key, bare, StringComparison.Ordinal))
            {
                continue;
            }

            if (SameType(existing.Value, target))
            {
                return;
            }

            throw new InvalidOperationException(
                "The using alias '" + bare + "' is already declared here for " +
                GlobalName(existing.Value) + ". Two aliases of one name in one declaration are CS1537.");
        }

        _aliases.Add(new KeyValuePair<string, ITypeDefinition>(bare!, target));
    }

    internal static bool SameType(ITypeDefinition left, ITypeDefinition right)
    {
        return ReferenceEquals(left, right) || left.Equals(right) ||
               string.Equals(GlobalName(left), GlobalName(right), StringComparison.Ordinal);
    }

    private static string GlobalName(ITypeDefinition type)
    {
        var builder = new StringBuilder();

        type.WriteTypeName(builder, TypeOutputMode.Global);

        return builder.ToString();
    }

    internal static bool IsIdentifier(string? name)
    {
        if (string.IsNullOrEmpty(name) || !(char.IsLetter(name![0]) || name[0] == '_'))
        {
            return false;
        }

        for (var i = 1; i < name.Length; i++)
        {
            if (!char.IsLetterOrDigit(name[i]) && name[i] != '_')
            {
                return false;
            }
        }

        return true;
    }
}
