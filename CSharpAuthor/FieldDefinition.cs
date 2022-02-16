namespace CSharpAuthor
{
    public class FieldDefinition : BaseOutputComponent
    {
        private readonly TypeDefinition _typeDefinition;
        private readonly string _name;

        public FieldDefinition(TypeDefinition typeDefinition, string name)
        {
            _typeDefinition = typeDefinition;
            _name = name;
        }
        
        public override void WriteOutput(IOutputContext outputContext)
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

            outputContext.WriteIndentedLine($"{accessModifier} {readonlyString}{staticString}{_typeDefinition.Name} {_name};");
        }
    }
}
