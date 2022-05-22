using System.Collections.Generic;

namespace CSharpAuthor
{
    public abstract class BaseOutputComponent : IOutputComponent
    {
        protected List<string> _usingNamespaces;

        public ComponentModifier Modifiers { get; set; } = ComponentModifier.None;

        public string Comment { get; set; }

        public void AddUsingNamespace(string ns)
        {
            if (_usingNamespaces == null)
            {
                _usingNamespaces = new List<string>();
            }

            _usingNamespaces.Add(ns);
        }

        public void WriteOutput(IOutputContext outputContext)
        {
            if (_usingNamespaces != null)
            {
                outputContext.AddImportNamespaces(_usingNamespaces);
            }

            WriteComponentOutput(outputContext);
        }

        protected abstract void WriteComponentOutput(IOutputContext outputContext);

        protected string GetAccessModifier(string defaultString)
        {
            if ((Modifiers & ComponentModifier.Public) == ComponentModifier.Public)
            {
                return KeyWords.Public;
            }

            if ((Modifiers & ComponentModifier.Protected) == ComponentModifier.Protected)
            {
                return KeyWords.Protected;
            }

            if ((Modifiers & ComponentModifier.Private) == ComponentModifier.Private)
            {
                return KeyWords.Private;
            }

            return defaultString;
        }
        
    }
}
