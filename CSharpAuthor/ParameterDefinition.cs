namespace CSharpAuthor
{
    public class ParameterDefinition : InstanceDefinition
    {
        public ParameterDefinition(ITypeDefinition typeDefinition, string name)
            : base(name)
        {
            TypeDefinition = typeDefinition;
        }

        public ITypeDefinition TypeDefinition { get; }

        public IOutputComponent? DefaultValue { get; set; }

        public void WriteWithSignature(IOutputContext outputContext)
        {
            outputContext.AddImportNamespace(TypeDefinition);

            outputContext.Write(TypeDefinition);
            outputContext.WriteSpace();
            outputContext.Write(Name);

            if (DefaultValue != null)
            {
                outputContext.Write(" = ");
                DefaultValue.WriteOutput(outputContext);
            }
        }

        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            outputContext.Write(Name);
        }
    }
}
