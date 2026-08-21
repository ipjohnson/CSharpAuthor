namespace CSharpAuthor;

public class ParameterDefinition : InstanceDefinition
{
    public ParameterDefinition(ITypeDefinition typeDefinition, string name)
        : base(name)
    {
        TypeDefinition = typeDefinition;
    }

    /// <summary>
    /// How the parameter is passed.
    /// </summary>
    public ParameterModifier Modifier { get; set; } = ParameterModifier.None;

    /// <summary>
    /// Whether the parameter is declared with <c>params</c>.
    /// </summary>
    /// <remarks>
    /// Only meaningful on the last parameter, and only for a type the caller can spread into. It is
    /// written as declared rather than validated here, matching how the rest of this library treats
    /// what it is handed.
    /// </remarks>
    public bool IsParams { get; set; } = false;

    /// <summary>
    /// Assigned by the callee. Shorthand for <see cref="Modifier"/>.
    /// </summary>
    public bool IsOut
    {
        get => Modifier == ParameterModifier.Out;
        set => Modifier = value ? ParameterModifier.Out : ParameterModifier.None;
    }

    /// <summary>
    /// Whether this is the receiver of an extension method. Combines with <see cref="Modifier"/>,
    /// since <c>this ref</c> and <c>this in</c> are both allowed on a struct receiver.
    /// </summary>
    public bool This { get; set; } = false;

    public ITypeDefinition TypeDefinition { get; }

    public IOutputComponent? DefaultValue { get; set; }

    public void WriteWithSignature(IOutputContext outputContext)
    {
        // Inline, because a parameter is part of a line rather than a line of its own. Attributes
        // could always be added to a parameter; they were simply never written.
        if (AttributeDefinitions != null)
        {
            foreach (var attributeDefinition in AttributeDefinitions)
            {
                attributeDefinition.WriteInline(outputContext);
            }
        }

        if (This)
        {
            outputContext.Write(KeyWords.This);
            outputContext.WriteSpace();
        }

        var modifier = GetModifierKeyword();

        if (modifier != null)
        {
            outputContext.Write(modifier);
            outputContext.WriteSpace();
        }

        if (IsParams)
        {
            outputContext.Write(KeyWords.Params);
            outputContext.WriteSpace();
        }

        outputContext.Write(TypeDefinition);
        outputContext.WriteSpace();
        outputContext.Write(CSharpIdentifier.Escape(Name));

        if (DefaultValue != null)
        {
            outputContext.Write(" = ");
            DefaultValue.WriteOutput(outputContext);
        }
    }

    /// <summary>
    /// The parameter as an argument at a call site, carrying the modifier the callee declared, for
    /// passing to <c>Invoke</c>, <c>New</c> and anything else that takes arguments.
    /// </summary>
    /// <remarks>
    /// Forwarding a call has to repeat <c>ref</c> and <c>out</c>; leaving them off does not compile.
    /// The parameter writes its bare name on its own, so one used as an ordinary value expression is
    /// unaffected.
    ///
    /// <c>in</c> is optional at a call site and <c>ref readonly</c> only warns without it, so neither
    /// is written: the argument reads the same either way.
    /// </remarks>
    public IOutputComponent AsArgument()
    {
        var modifier = Modifier switch
        {
            ParameterModifier.Ref => KeyWords.Ref,
            ParameterModifier.Out => KeyWords.Out,
            _ => null
        };

        var name = CSharpIdentifier.Escape(Name);

        return new CodeOutputComponent(modifier == null ? name : modifier + " " + name)
        {
            Indented = false
        };
    }

    private string? GetModifierKeyword()
    {
        return Modifier switch
        {
            ParameterModifier.Ref => KeyWords.Ref,
            ParameterModifier.Out => KeyWords.Out,
            ParameterModifier.In => KeyWords.In,
            ParameterModifier.RefReadOnly => KeyWords.Ref + " " + KeyWords.ReadOnly,
            _ => null
        };
    }

    /// <summary>
    /// The parameter used as a value expression - its own name.
    /// </summary>
    /// <remarks>
    /// Escaped the same way the declaration is, so a parameter named after a keyword reads back as
    /// the identifier it was declared as.
    /// </remarks>
    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.Write(CSharpIdentifier.Escape(Name));
    }
}
