namespace CSharpAuthor;

/// <summary>
/// A constructor: a <see cref="MethodDefinition"/> named after its type and with no return type.
/// </summary>
/// <remarks>
/// Parameters and a body are added the same way a method's are. The two things a constructor has
/// that a method does not are the initialiser - <see cref="Base"/> - and
/// <see cref="IsPrimary"/>, which moves the parameter list into the type header.
/// </remarks>
public class ConstructorDefinition : MethodDefinition
{
    /// <summary>
    /// The initialiser written after the signature: <c>: base(name)</c> or <c>: this(name)</c>.
    /// </summary>
    /// <remarks>
    /// Set through the constructor rather than assignable, and built with
    /// <see cref="SyntaxHelpers.Base"/> or <see cref="SyntaxHelpers.This"/>. A record passing
    /// arguments to a positional base uses
    /// <see cref="ClassDefinition.AddBaseType(ITypeDefinition, IOutputComponent[])"/> instead -
    /// the arguments belong to the base type clause there, not to a constructor.
    /// </remarks>
    public IOutputComponent? Base { get; }

    /// <summary>
    /// A constructor for the type named <paramref name="name"/>. Prefer
    /// <see cref="ClassDefinition.AddConstructor"/>, which builds one with the right name and
    /// attaches it.
    /// </summary>
    public ConstructorDefinition(string name, IOutputComponent? @base = null) : base(name)
    {
        Base = @base;
    }

    /// <summary>
    /// Whether this is the type's primary constructor, declared in the type header rather than as a
    /// member of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The parameter list moves to the type declaration - <c>public record Pet(string Id)</c> - and
    /// the constructor writes nothing of its own. On a record the parameters become public
    /// properties by language rule, so nothing needs to be declared for them; on a class they are
    /// captured and any exposure is the caller's to write.
    /// </para>
    /// <para>
    /// A type has at most one. Setting it on a second constructor is not checked here, the same way
    /// nothing else in this library validates what it is handed; the compiler reports it.
    /// </para>
    /// </remarks>
    public bool IsPrimary { get; set; }

    protected override void WriteReturnType(IOutputContext outputContext)
    {
        // constructors don't have return Types
    }

    protected override void WriteAccessModifier(IOutputContext outputContext)
    {
        outputContext.WriteIndent();

        if ((Modifiers & ComponentModifier.Static) == ComponentModifier.Static)
        {
            outputContext.Write(KeyWords.Static);
        }
        else
        {
            outputContext.Write(GetAccessModifier(KeyWords.Public));
        }
    }

    protected override void WriteEndOfMethodSignature(IOutputContext outputContext)
    {
        base.WriteEndOfMethodSignature(outputContext);

        if (Base != null)
        {
            outputContext.WriteIndent();
            outputContext.Write(outputContext.SingleIndent);
            outputContext.Write(" : ");
            Base.WriteOutput(outputContext);
            outputContext.WriteLine();
        }
    }
}