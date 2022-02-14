using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpAuthor
{
    public abstract class BaseOutputComponent : IOutputComponent
    {
        public ComponentModifier Modifiers { get; set; }

        public string? Comment { get; set; }

        public abstract void WriteOutput(IOutputContext outputContext);

        public abstract void GetKnownTypes(List<TypeDefinition> types);

        protected string GetAccessModifier(string defaultString)
        {
            if ((Modifiers | ComponentModifier.Public) == ComponentModifier.Public)
            {
                return KeyWords.Public;
            }

            if ((Modifiers | ComponentModifier.Protected) == ComponentModifier.Protected)
            {
                return KeyWords.Protected;
            }

            if ((Modifiers | ComponentModifier.Private) == ComponentModifier.Private)
            {
                return KeyWords.Private;
            }

            return defaultString;
        }
    }
}
