using CSharpAuthor.Profiles;

namespace CSharpAuthor;

/// <summary>
/// The <c>field</c> contextual keyword inside a property accessor - the compiler-generated backing
/// field, without one having to be declared.
/// </summary>
/// <remarks>
/// <para>
/// C# 14, and <see cref="DownlevelSupport.Policy"/>: below it there is no keyword, and the
/// alternative is a real backing field, which an expression cannot conjure into the containing
/// type. So the fallback name is the caller's to give and the field is the caller's to declare -
/// that is what Policy means.
/// </para>
/// <para>
/// It is written as a component rather than as <c>Code("field")</c> because below C# 14
/// <c>field</c> is an ordinary identifier: it binds to whatever <c>field</c> happens to be in
/// scope, or fails with CS0103. Either way the generated property silently stops referring to its
/// own storage, which is the failure this asks the profile about rather than guessing at.
/// </para>
/// </remarks>
public class FieldKeywordComponent : BaseOutputComponent
{
    private readonly string _backingFieldName;
    private readonly string? _context;

    /// <param name="backingFieldName">
    /// What to write when the target is below C# 14. The caller declares this field; nothing here
    /// adds it to the containing type.
    /// </param>
    /// <param name="context">
    /// The property's name, for the diagnostic and the <c>// DOWNLEVEL:</c> comment.
    /// </param>
    public FieldKeywordComponent(string backingFieldName, string? context = null)
    {
        _backingFieldName = backingFieldName;
        _context = context;
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        // No output context is passed, because this is written mid-expression and a
        // `// DOWNLEVEL:` comment is a line of its own - it would land inside the accessor body.
        // The diagnostic still records it.
        var mayEmit = outputContext.EmitSession()
            .MayEmit(LanguageFeature.FieldKeyword, _context ?? _backingFieldName);

        outputContext.Write(
            mayEmit ? KeyWords.Field : CSharpIdentifier.Escape(_backingFieldName));
    }
}
