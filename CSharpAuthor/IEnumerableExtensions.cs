using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
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
            var writeComma = false;

            foreach (var tValue in components)
            {
                if (writeComma)
                {
                    context.Write(", ");
                }
                else
                {
                    writeComma = true;
                }

                writeAction(context, tValue);
            }
        }
    }
}
