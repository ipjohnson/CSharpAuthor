namespace CSharpAuthor;

/// <summary>
/// A field: <c>private readonly string _name;</c>.
/// </summary>
/// <remarks>
/// The one declaration in this library that defaults to <c>private</c> rather than <c>public</c>,
/// because that is what a field almost always is. Add <see cref="ComponentModifier.Readonly"/> and
/// <see cref="ComponentModifier.Static"/> through <see cref="BaseOutputComponent.Modifiers"/>; they
/// are written in the order C# convention puts them - <c>static readonly</c> - whichever order the
/// flags were set in.
/// </remarks>
public class FieldDefinition : BaseOutputComponent, INamedComponent
{
    /// <summary>
    /// A field of <paramref name="typeDefinition"/> named <paramref name="name"/>. Prefer
    /// <see cref="ClassDefinition.AddField(ITypeDefinition, string)"/>, which builds one, attaches
    /// it, and rejects a name already in use.
    /// </summary>
    public FieldDefinition(ITypeDefinition typeDefinition, string name)
    {
        TypeDefinition = typeDefinition;
        Name = name;
    }

    /// <summary>The field's type.</summary>
    public ITypeDefinition TypeDefinition { get; }
    
    /// <summary>The declared name, escaped with <c>@</c> if it is a keyword.</summary>
    public string Name { get; }

    /// <summary>
    /// The initialiser: <c>private readonly List&lt;string&gt; _items = new List&lt;string&gt;();</c>.
    /// </summary>
    /// <remarks>
    /// A component rather than a string, so a <c>new</c> of a type reaches the file as a type and
    /// brings its namespace with it - see <see cref="SyntaxHelpers.New(ITypeDefinition, object[])"/>.
    /// Unlike <see cref="PropertyDefinition.DefaultValue"/>, this is always written; a field has no
    /// shape that could displace it.
    /// </remarks>
    public IOutputComponent? InitializeValue { get; set; }

    /// <summary>
    /// The field used as a value expression - its own name - for building statements that read or
    /// assign it.
    /// </summary>
    /// <remarks>
    /// A fresh instance each time; it is a name, not a reference to this declaration. Holding it
    /// rather than repeating the string is what keeps a constructor body and the field it assigns
    /// in step.
    /// </remarks>
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