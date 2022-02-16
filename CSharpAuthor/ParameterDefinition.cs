namespace CSharpAuthor
{
    public class ParameterDefinition : BaseOutputComponent
    {
        private readonly TypeDefinition typeDefinition;
        private readonly string name;

        public ParameterDefinition(TypeDefinition typeDefinition, string name)
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
