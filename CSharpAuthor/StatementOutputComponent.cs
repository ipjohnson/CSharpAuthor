using System.Collections.Generic;

namespace CSharpAuthor
{
    public class StatementOutputComponent : BaseOutputComponent
    {
        private string _statement;
        private List<ITypeDefinition> _typeDefinitions;

        public StatementOutputComponent(string statement)
        {
            _statement = statement;
        }

        public void AddType(ITypeDefinition typeDefinition)
        {
            if (_typeDefinitions == null)
            {
                _typeDefinitions = new List<ITypeDefinition>();
            }

            _typeDefinitions.Add(typeDefinition);
        }

        public void AddTypes(IEnumerable<ITypeDefinition> typeDefinitions)
        {
            if (_typeDefinitions == null)
            {
                _typeDefinitions = new List<ITypeDefinition>();
            }

            _typeDefinitions.AddRange(typeDefinitions);
        }
        public override void WriteOutput(IOutputContext outputContext)
        {
            outputContext.WriteIndentedLine(_statement);

            if (_typeDefinitions != null)
            {
                outputContext.AddImportNamespace(_typeDefinitions);
            }
        }
    }
}
