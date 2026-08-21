using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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

    /// <summary>
    /// The pieces this was built from, for a caller that is composing a larger statement out of
    /// them - <c>AddCode</c> substituting a value into a line. Null when the statement is only text.
    /// </summary>
    internal IReadOnlyList<object>? Parts => _parts;

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
        // Before the string check: an enum is not IEnumerable<string>, but it is the one value whose
        // ToString() looks like working C# and is not.
        if (value is Enum enumValue)
        {
            return GetEnumValue(enumValue, indented);
        }

        if (value is IEnumerable<string> stringValues)
        {
            return GetNewStringArray(stringValues, indented);
        }

        if (value is Array { Rank: > 1 } multiDimensional)
        {
            return GetNewMultiDimensionalArray(multiDimensional, indented);
        }

        if (value is Array values)
        {
            return GetNewArray(values, indented);
        }

        // Every scalar goes through the one formatter: culture invariant, suffixed so the literal
        // denotes the type it came from, and quoted where C# requires quotes. A bare `1.5` for a
        // float is CS0664 and a bare `a` for a char is CS0103 - both were emitted here.
        return new CodeOutputComponent(LiteralFormatter.Format(value)) { Indented = indented };
    }

    /// <summary>
    /// An enum value as the C# that denotes it - <c>Lifetime.Singleton</c>, with the type left
    /// unrendered so the file derives the namespace it needs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the section 1 defect at its source. The value used to fall through to
    /// <c>ToString()</c>, which answers with the member name alone: <c>Singleton</c>. That is
    /// CS0103 in the ordinary case, and where a local of that name is in scope it compiles and
    /// means something else entirely. Nothing recorded the namespace either, so even the qualified
    /// reading had nothing to resolve against.
    /// </para>
    /// <para>
    /// Three shapes. A named member is <c>Type.Member</c>. A combination of flags is
    /// <c>Type.A | Type.B</c> - <c>ToString()</c> gives <c>"A, B"</c> for it, which is a list, not
    /// an expression. A value with no name at all is a cast of its number, which is what C# has for
    /// it and what a round trip through the enum would produce.
    /// </para>
    /// </remarks>
    private static IOutputComponent GetEnumValue(Enum value, bool indented)
    {
        var enumType = TypeDefinition.Get(value.GetType());
        var text = value.ToString();

        if (text.Length > 0 && (char.IsLetter(text[0]) || text[0] == '_'))
        {
            var names = text.Split(new[] { ", " }, StringSplitOptions.None);
            var parts = new List<object>(names.Length * 3);

            for (var i = 0; i < names.Length; i++)
            {
                if (i > 0)
                {
                    parts.Add(" | ");
                }

                parts.Add(enumType);
                parts.Add("." + CSharpIdentifier.Escape(names[i]));
            }

            return WithIndent(FromParts(parts), indented);
        }

        var underlying = Convert.ChangeType(
            value, Enum.GetUnderlyingType(value.GetType()), CultureInfo.InvariantCulture);

        return WithIndent(
            FromParts(new object[] { "(", enumType, ")", LiteralFormatter.FormatNumeric(underlying) }),
            indented);
    }

    /// <summary>
    /// A rank-2-or-higher array as a C# array creation of the same shape.
    /// </summary>
    /// <remarks>
    /// Every array used to go through the rank-1 path, which iterates - and iterating a rank-2
    /// array yields its elements in row-major order with nothing to say where the rows ended. A
    /// <c>new int[2,2] { { 1, 2 }, { 3, 4 } }</c> came out as <c>new int[] { 1, 2, 3, 4 }</c>: a
    /// different value, of a different type, that only fails to compile if something happens to
    /// assign it to an <c>int[,]</c>.
    /// </remarks>
    private static IOutputComponent GetNewMultiDimensionalArray(Array values, bool indented)
    {
        var elementType = values.GetType().GetElementType();
        var parts = new List<object>
        {
            "new ",
            TypeDefinition.Get(elementType ?? typeof(object)),
            "[" + new string(',', values.Rank - 1) + "] "
        };

        var indices = new int[values.Rank];

        AppendArrayDimension(parts, values, indices, 0);

        return WithIndent(FromParts(parts), indented);
    }

    private static void AppendArrayDimension(
        List<object> parts, Array values, int[] indices, int dimension)
    {
        parts.Add("{ ");

        var lower = values.GetLowerBound(dimension);
        var upper = values.GetUpperBound(dimension);

        for (var i = lower; i <= upper; i++)
        {
            if (i > lower)
            {
                parts.Add(", ");
            }

            indices[dimension] = i;

            if (dimension == values.Rank - 1)
            {
                var element = values.GetValue(indices);

                if (element is ITypeDefinition typeDefinition)
                {
                    parts.Add(typeDefinition);
                }
                else
                {
                    parts.Add(LiteralFormatter.Format(element));
                }
            }
            else
            {
                AppendArrayDimension(parts, values, indices, dimension + 1);
            }
        }

        parts.Add(" }");
    }

    private static CodeOutputComponent WithIndent(CodeOutputComponent component, bool indented)
    {
        component.Indented = indented;

        return component;
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

    /// <summary>
    /// Says that this statement mentions a type without writing it as one.
    /// </summary>
    /// <remarks>
    /// Kept for callers written against version 1. It cannot do what
    /// <see cref="Get(ITypeDefinition, string, bool)"/> and <see cref="FromParts"/> do - the type is
    /// still text by the time it gets here, so it cannot be qualified or aliased - and all it can
    /// still offer is the namespace, in the one mode where a namespace is what makes the name
    /// resolve. Prefer handing the type over instead of its name.
    /// </remarks>
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

        // Only where a namespace is what makes an unqualified name resolve. In a mode that
        // qualifies, the name in this statement is text and no directive can make it right - which
        // is the whole reason the type should have been handed over rather than named.
        if (_typeDefinitions != null && outputContext.Options.TypeOutputMode == TypeOutputMode.ShortName)
        {
            foreach (var typeDefinition in _typeDefinitions)
            {
                outputContext.AddImportNamespaces(typeDefinition.KnownNamespaces);
            }
        }
    }
    public static implicit operator CodeOutputComponent(string statement) => new(statement);
}
