using System.Collections.Generic;

namespace CSharpAuthor
{
    public class StatementOutputComponent : BaseOutputComponent
    {
        private readonly string _statement;
        private List<ITypeDefinition>? _typeDefinitions;

        public StatementOutputComponent(string statement)
        {
            _statement = statement;
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
            if (Indented)
            {
                outputContext.WriteIndentedLine(_statement);
            }
            else
            {
                outputContext.Write(_statement);
            }

            if (_typeDefinitions != null)
            {
                outputContext.AddImportNamespaces(_typeDefinitions);
            }
        }
    }
}
