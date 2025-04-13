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
        var writeSeparator = false;

        if (newLineBeforeItems)
        {
            context.IncrementIndent();
        }
        
        IReadOnlyList<T>? list = components as IReadOnlyList<T> ?? components.ToList();

        foreach (var tValue in list)
        {
            if (writeSeparator)
            {
                context.Write(separator);
            }
            else
            {
                writeSeparator = true;
            }
            
            if (newLineBeforeItems && list.Count > 1)
            {
                context.WriteLine();
                context.WriteIndent();
            }
            
            writeAction(context, tValue);
        }
        
        if (newLineBeforeItems)
        {
            if (list.Count > 1)
            {
                context.WriteLine();
            }
            
            context.DecrementIndent();

            if (list.Count > 0)
            {
                context.WriteIndent();
            }
        }
    }
}