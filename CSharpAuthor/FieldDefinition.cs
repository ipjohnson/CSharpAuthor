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

    public IOutputComponent? InitializeValue { get; set; }

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

        outputContext.WriteIndent($"{accessModifier} {readonlyString}{staticString}");
        outputContext.Write(TypeDefinition);
        outputContext.Write($" {Name}");

        if (InitializeValue != null)
        {
            outputContext.Write(" = ");
            InitializeValue.WriteOutput(outputContext);
        }
        
        outputContext.WriteLine(";");
    }
}