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
        var accessModifier = GetAccessModifier(KeyWords.Private);

        // `static readonly`, which is the order C# convention uses. This wrote `readonly static`,
        // which compiles and reads as a transcription error.
        var modifiers = Modifiers.GetModifierKeywords(
            ComponentModifier.Static | ComponentModifier.Readonly);

        // The space belongs to the keyword, not to the position. Written unconditionally it left a
        // member declared without accessibility indented one column too far - `     int f;` - which
        // compiles and shows up in the diff of every snapshot it appears in.
        outputContext.WriteIndent(
            accessModifier.Length > 0 ? accessModifier + " " + modifiers : modifiers);
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