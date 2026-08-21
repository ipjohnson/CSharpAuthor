using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class CodeOutputComponent : BaseOutputComponent
{
    private readonly string _statement;

    /// <summary>
    /// The statement in pieces - strings and <em>unrendered</em> types - when it has a type in it.
    /// Null when it is only text, which is the common case and stays a single string.
    /// </summary>
    private readonly IReadOnlyList<object>? _parts;

    private List<ITypeDefinition>? _typeDefinitions;

    public CodeOutputComponent(string statement)
    {
        _statement = statement;
    }

    /// <summary>
    /// A statement that is a type, optionally followed by a member of it - <c>ServiceLifetime</c>,
    /// or <c>ServiceLifetime.Transient</c>.
    /// </summary>
    /// <remarks>
    /// The point of it is the namespace. Written as the string <c>"ServiceLifetime.Transient"</c>
    /// this tracks nothing, so the name only resolves if some other part of the file happens to
    /// have brought the namespace in - and in a file that qualifies every type it writes, nothing
    /// does. Handed the type, it is written like any other type reference: qualified when the mode
    /// qualifies, aliased when the name is contested, and counted when the using list is worked out.
    /// </remarks>
    public CodeOutputComponent(ITypeDefinition typeDefinition, string? memberName = null)
    {
        if (typeDefinition == null)
        {
            throw new ArgumentNullException(nameof(typeDefinition));
        }

        _parts = string.IsNullOrEmpty(memberName)
            ? new object[] { typeDefinition }
            : new object[] { typeDefinition, "." + memberName };

        _statement = typeDefinition.GetShortName() + (string.IsNullOrEmpty(memberName) ? "" : "." + memberName);
    }

    private CodeOutputComponent(IReadOnlyList<object> parts, string statement)
    {
        _parts = parts;
        _statement = statement;
    }

    /// <summary>
    /// Builds a statement out of pieces, each either a string or an <see cref="ITypeDefinition"/>
    /// that stays unrendered until the file is serialized.
    /// </summary>
    public static CodeOutputComponent FromParts(IReadOnlyList<object> parts)
    {
        if (parts == null)
        {
            throw new ArgumentNullException(nameof(parts));
        }

        var builder = new StringBuilder();
        var hasType = false;

        foreach (var part in parts)
        {
            if (part is ITypeDefinition typeDefinition)
            {
                hasType = true;
                builder.Append(typeDefinition.GetShortName());
            }
            else
            {
                builder.Append(part);
            }
        }

        var statement = builder.ToString();

        return hasType ? new CodeOutputComponent(parts, statement) : new CodeOutputComponent(statement);
    }

    public static IEnumerable<IOutputComponent> GetAll(IEnumerable<object> values, bool indented = false)
    {
        foreach (var objectValue in values)
        {
            yield return Get(objectValue, indented);
        }
    }

    public static IOutputComponent Get(object? value, bool indented = false)
    {
        return value switch
        {
            null => new CodeOutputComponent("") { Indented = indented },

            IOutputComponent outputComponent => outputComponent,

            // A type stays a type all the way to serialization rather than becoming its name here.
            ITypeDefinition typeDefinition => new CodeOutputComponent(typeDefinition) { Indented = indented },

            _ => DefaultComponent(value, indented)
        };
    }

    /// <summary>
    /// A member reached off a type, with the type left unrendered - <c>ServiceLifetime.Transient</c>
    /// written so that the <c>ServiceLifetime</c> half knows where it comes from.
    /// </summary>
    public static IOutputComponent Get(ITypeDefinition typeDefinition, string memberName, bool indented = false)
    {
        return new CodeOutputComponent(typeDefinition, memberName) { Indented = indented };
    }

    private static IOutputComponent DefaultComponent(object value, bool indented) {
        if (value is IEnumerable<string> stringValues)
        {
            return GetNewStringArray(stringValues, indented);
        }

        if (value is Array values)
        {
            return GetNewArray(values, indented);
        }

        if (value is bool booleanValue)
        {
            return new CodeOutputComponent(booleanValue ? "true" : "false") { Indented = indented };
        }

        return new CodeOutputComponent(value.ToString()) { Indented = indented };
    }

    private static IOutputComponent GetNewStringArray(IEnumerable<string> stringValues, bool indented)
    {
        var values = new List<IOutputComponent>();

        foreach (var stringValue in stringValues)
        {
            values.Add(Get(SyntaxHelpers.QuoteString(stringValue)));
        }

        return new NewArrayStatement(TypeDefinition.Get(typeof(string)), values.ToArray());
    }

    private static IOutputComponent GetNewArray(IEnumerable values, bool indented)
    {
        var outputComponents = new List<IOutputComponent>();

        Type? type = null;

        if (values is Array array)
        {
            type = array.GetType().GetElementType();
        }

        foreach (var value in values)
        {
            if (type == null)
            {
                type = value.GetType();
            }

            outputComponents.Add(Get(value));
        }

        return new NewArrayStatement(
            TypeDefinition.Get(type ?? typeof(object)), outputComponents.ToArray());
    }

    public void AddType(ITypeDefinition typeDefinition)
    {
        _typeDefinitions ??= new List<ITypeDefinition>();

        _typeDefinitions.Add(typeDefinition);
    }

    public void AddTypes(IEnumerable<ITypeDefinition> typeDefinitions)
    {
        _typeDefinitions ??= new List<ITypeDefinition>();

        _typeDefinitions.AddRange(typeDefinitions);
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        if (_parts == null)
        {
            if (Indented)
            {
                outputContext.WriteIndentedLine(_statement);
            }
            else
            {
                outputContext.Write(_statement);
            }
        }
        else
        {
            if (Indented)
            {
                outputContext.WriteIndent();
            }

            for (var i = 0; i < _parts.Count; i++)
            {
                if (_parts[i] is ITypeDefinition typeDefinition)
                {
                    outputContext.Write(typeDefinition);
                }
                else
                {
                    outputContext.Write((string)_parts[i]);
                }
            }

            if (Indented)
            {
                outputContext.WriteLine();
            }
        }

        if (_typeDefinitions != null)
        {
            outputContext.AddImportNamespaces(_typeDefinitions);
        }

    }
    public static implicit operator CodeOutputComponent(string statement) => new(statement);
}
