using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpAuthor;

public static class IEnumerableExtensions
{
    public static void OutputCommaSeparatedList(this IEnumerable<ITypeDefinition> components, IOutputContext context, bool newLineBeforeItems = false)
    {
        OutputCommaSeparatedList(components, context, (outputContext, definition) => outputContext.Write(definition), newLineBeforeItems);
    }

    public static void OutputCommaSeparatedList(this IEnumerable<IOutputComponent> components, IOutputContext context, bool newLineBeforeItems = false)
    {
        OutputCommaSeparatedList(components, context, (outputContext, component) => component.WriteOutput(outputContext), newLineBeforeItems);
    }

    public static void OutputCommaSeparatedList<T>(this IEnumerable<T> components, IOutputContext context, Action<IOutputContext, T> writeAction, bool newLineBeforeItems = false)
    {
        OutputSeparatedList(components, context, writeAction, ", ", newLineBeforeItems);
    }

    public static void OutputSeparatedList<T>(this IEnumerable<T> components, IOutputContext context, Action<IOutputContext, T> writeAction, string separator, bool newLineBeforeItems = false)
    {
        IReadOnlyList<T> list = components as IReadOnlyList<T> ?? components.ToList();

        // A list of one stays on the line it was opened on. Indenting for a break that never
        // happens leaves the closing bracket stranded mid-line, which is what a single argument
        // wrapping a broken one used to produce: Intercept(new Context(\n ... \n    )    );
        var breakLines = newLineBeforeItems && list.Count > 1;

        if (breakLines)
        {
            context.IncrementIndent();
        }

        // The line break already separates the items, so the separator does not also pad the end of
        // the line it terminates. Trimmed once rather than once per item: TrimEnd builds a string,
        // and a twenty-five parameter list was building twenty-four identical ones.
        var itemSeparator = breakLines ? separator.TrimEnd() : separator;

        // Indexed rather than foreach: the list is behind IReadOnlyList<T>, so its enumerator is
        // reached through the interface and allocated, once per list written.
        for (var i = 0; i < list.Count; i++)
        {
            if (i > 0)
            {
                context.Write(itemSeparator);
            }

            if (breakLines)
            {
                context.WriteLine();
                context.WriteIndent();
            }

            writeAction(context, list[i]);
        }

        if (breakLines)
        {
            context.WriteLine();
            context.DecrementIndent();
            context.WriteIndent();
        }
    }
}