namespace CSharpAuthor;

public class FieldDefinition : BaseOutputComponent, INamedComponent
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

        // `static readonly`, which is the order C# convention uses. This wrote `readonly static`,
        // which compiles and reads as a transcription error.
        var modifiers = Modifiers.GetModifierKeywords(
            ComponentModifier.Static | ComponentModifier.Readonly);

        outputContext.WriteIndent($"{accessModifier} {modifiers}");
        outputContext.Write(TypeDefinition);
        outputContext.Write(" " + CSharpIdentifier.Escape(Name));

        if (InitializeValue != null)
        {
            outputContext.Write(" = ");
            InitializeValue.WriteOutput(outputContext);
        }
        
        outputContext.WriteLine(";");
    }

    protected override void WriteComment(IOutputContext outputContext)
    {
        if (Comment == null)
        {
            return;
        }
        
        DocumentationComment.WriteSummary(outputContext.WriteLine, Comment);
    }
}