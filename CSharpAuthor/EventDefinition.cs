using System;

namespace CSharpAuthor;

/// <summary>
/// An event, written either as a field-like declaration or with add and remove accessors.
/// </summary>
/// <remarks>
/// <c>public event EventHandler Changed;</c> when neither accessor has a body, and
/// <c>public event EventHandler Changed { add { } remove { } }</c> when either does. A type
/// implementing an interface that declares an event has to declare one too, so a generated wrapper
/// cannot be written without this.
/// </remarks>
public class EventDefinition : BaseOutputComponent, INamedComponent
{
    /// <summary>
    /// An event of <paramref name="handlerType"/> named <paramref name="name"/>. Prefer
    /// <see cref="ClassDefinition.AddEvent(ITypeDefinition, string)"/>, which builds one and
    /// attaches it.
    /// </summary>
    public EventDefinition(ITypeDefinition handlerType, string name)
    {
        HandlerType = handlerType;
        Name = name;

        Add = new PropertyMethodDefinition();
        Remove = new PropertyMethodDefinition();
    }

    /// <summary>The declared name, escaped with <c>@</c> if it is a keyword.</summary>
    public string Name { get; }

    /// <summary>
    /// The delegate type the event is declared with.
    /// </summary>
    public ITypeDefinition HandlerType { get; }

    /// <summary>
    /// The <c>add</c> accessor. Giving it statements is what turns the field-like declaration into
    /// an accessor pair.
    /// </summary>
    public PropertyMethodDefinition Add { get; }

    /// <summary>
    /// The <c>remove</c> accessor. C# requires both accessors or neither, so giving statements to
    /// one means giving them to the other.
    /// </summary>
    public PropertyMethodDefinition Remove { get; }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        var accessModifier = GetAccessModifier(KeyWords.Public);

        outputContext.WriteIndent();

        if (!string.IsNullOrEmpty(accessModifier))
        {
            outputContext.Write(accessModifier);
            outputContext.WriteSpace();
        }

        outputContext.Write(
            Modifiers.GetModifierKeywords(ComponentModifierExtensions.PropertyModifiers));

        outputContext.Write("event ");
        outputContext.Write(HandlerType);
        outputContext.WriteSpace();
        outputContext.Write(CSharpIdentifier.Escape(Name));

        // Without a body on either accessor this is a field-like event, which is the shape almost
        // every event is declared in.
        if (Add.StatementCount == 0 && Remove.StatementCount == 0)
        {
            outputContext.WriteLine(";");

            return;
        }

        outputContext.WriteLine();
        outputContext.OpenScope();

        outputContext.WriteIndent("add");
        Add.WriteOutput(outputContext);

        outputContext.WriteIndent("remove");
        Remove.WriteOutput(outputContext);

        outputContext.CloseScope();
    }

    protected override void WriteComment(IOutputContext outputContext)
    {
        if (string.IsNullOrWhiteSpace(Comment))
        {
            return;
        }

        DocumentationComment.WriteSummary(outputContext.WriteIndentedLine, Comment);
    }
}
