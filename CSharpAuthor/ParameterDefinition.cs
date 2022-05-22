namespace CSharpAuthor
{
    public class ParameterDefinition : BaseOutputComponent
    {
        public ParameterDefinition(ITypeDefinition typeDefinition, string name)
        {
            TypeDefinition = typeDefinition;
            Name = name;
        }

        public string Name { get; }

        public ITypeDefinition TypeDefinition { get; }

        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            outputContext.AddImportNamespace(TypeDefinition);

            outputContext.Write(TypeDefinition.Name);
            outputContext.WriteSpace();
            outputContext.Write(Name);
        }
    }
}
