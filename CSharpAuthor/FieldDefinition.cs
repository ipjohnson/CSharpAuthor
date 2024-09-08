namespace CSharpAuthor;

public class FieldDefinition : BaseOutputComponent
{
        
    public FieldDefinition(ITypeDefinition typeDefinition, string name)
    {
        TypeDefinition = typeDefinition;
        Name = name;
    }

    public ITypeDefinition TypeDefinition { get; }
    
    public string Name { get; }

    public string? InitializeValue { get; set; }

    public InstanceDefinition Instance => new (Name);

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.AddImportNamespace(TypeDefinition);

        var accessModifier = GetAccessModifier(KeyWords.Private);
        var readonlyString = "";
        var staticString = "";

        if ((Modifiers & ComponentModifier.Static) == ComponentModifier.Static)
        {
            staticString = KeyWords.Static + " ";
        }

        if ((Modifiers & ComponentModifier.Readonly) == ComponentModifier.Readonly)
        {
            readonlyString = KeyWords.ReadOnly + " ";
        }

        var initValue = "";

        if (!string.IsNullOrEmpty(InitializeValue))
        {
            initValue = $" = {InitializeValue}";
        }

        outputContext.WriteIndent($"{accessModifier} {readonlyString}{staticString}");
        outputContext.Write(TypeDefinition);
        outputContext.WriteLine($" {Name}{initValue};");
    }
}