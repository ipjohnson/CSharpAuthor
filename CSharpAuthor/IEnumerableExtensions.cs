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

        var writeSeparator = false;

        foreach (var tValue in list)
        {
            if (writeSeparator)
            {
                // The line break already separates the items, so the separator does not also pad
                // the end of the line it terminates.
                context.Write(breakLines ? separator.TrimEnd() : separator);
            }
            else
            {
                writeSeparator = true;
            }

            if (breakLines)
            {
                context.WriteLine();
                context.WriteIndent();
            }

            writeAction(context, tValue);
        }

        if (breakLines)
        {
            context.WriteLine();
            context.DecrementIndent();
            context.WriteIndent();
        }
    }
}