using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public static class IEnumerableExtensions
{
    public static void OutputCommaSeparatedList(this IEnumerable<ITypeDefinition> components, IOutputContext context)
    {
        OutputCommaSeparatedList(components, context, (outputContext, definition) => outputContext.Write(definition));
    }

    public static void OutputCommaSeparatedList(this IEnumerable<IOutputComponent> components, IOutputContext context)
    {
        OutputCommaSeparatedList(components, context, (outputContext, component) => component.WriteOutput(outputContext));   
    }

    public static void OutputCommaSeparatedList<T>(this IEnumerable<T> components, IOutputContext context, Action<IOutputContext, T> writeAction)
    {
        OutputSeparatedList(components, context, writeAction, ", ");
    }

    public static void OutputSeparatedList<T>(this IEnumerable<T> components, IOutputContext context, Action<IOutputContext, T> writeAction, string separator)
    {
        var writeSeparator = false;

        foreach (var tValue in components)
        {
            if (writeSeparator)
            {
                context.Write(separator);
            }
            else
            {
                writeSeparator = true;
            }

            writeAction(context, tValue);
        }
    }
}