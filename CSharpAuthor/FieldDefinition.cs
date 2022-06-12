namespace CSharpAuthor
{
    public class FieldDefinition : BaseOutputComponent
    {
        private readonly ITypeDefinition _typeDefinition;
        
        public FieldDefinition(ITypeDefinition typeDefinition, string name)
        {
            _typeDefinition = typeDefinition;
            Name = name;
        }

        public string Name { get; }

        public string? InitializeValue { get; set; }
        
        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            outputContext.AddImportNamespace(_typeDefinition);

            var accessModifier = GetAccessModifier(KeyWords.Private);
            var readonlyString = "";
            var staticString = "";

            if ((Modifiers & ComponentModifier.Readonly) == ComponentModifier.Readonly)
            {
                readonlyString = KeyWords.ReadOnly + " ";
            }

            if ((Modifiers & ComponentModifier.Static) == ComponentModifier.Static)
            {
                staticString = KeyWords.Static + " ";
            }

            var initValue = "";

            if (!string.IsNullOrEmpty(InitializeValue))
            {
                initValue = $" = {InitializeValue}";
            }

            outputContext.WriteIndent($"{accessModifier} {readonlyString}{staticString}");
            outputContext.Write(_typeDefinition);
            outputContext.WriteLine($" {Name}{initValue};");
        }
    }
}
