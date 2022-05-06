﻿namespace CSharpAuthor
{
    public class FieldDefinition : BaseOutputComponent
    {
        private readonly TypeDefinition _typeDefinition;
        
        public FieldDefinition(TypeDefinition typeDefinition, string name)
        {
            _typeDefinition = typeDefinition;
            Name = name;
        }

        public string Name { get; }

        public string InitializeValue { get; set; }
        
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

            var initValue = "";

            if (!string.IsNullOrEmpty(InitializeValue))
            {
                initValue = $" = {InitializeValue}";
            }

            outputContext.WriteIndentedLine($"{accessModifier} {readonlyString}{staticString}{_typeDefinition.Name} {Name}{initValue};");
        }
    }
}
