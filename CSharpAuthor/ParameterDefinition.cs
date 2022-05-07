namespace CSharpAuthor
{
    public class ParameterDefinition : BaseOutputComponent
    {
        private readonly ITypeDefinition typeDefinition;
        private readonly string name;

        public ParameterDefinition(ITypeDefinition typeDefinition, string name)
        {
            this.typeDefinition = typeDefinition;
            this.name = name;
        }

        public override void WriteOutput(IOutputContext outputContext)
        {
            outputContext.AddImportNamespace(typeDefinition);

            outputContext.Write(typeDefinition.Name);
            outputContext.WriteSpace();
            outputContext.Write(name);
        }
    }
}
