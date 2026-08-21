using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// An enum and its members: <c>public enum Level { Low, High = 10, }</c>.
/// </summary>
/// <remarks>
/// Members are written in the order they are added, each terminated with a comma - the trailing one
/// included, which C# allows and which keeps a one-member diff to one line.
/// </remarks>
public class EnumDefinition : BaseOutputComponent, INamedComponent
{
    private readonly List<EnumValueDefinition> _enumValueDefinitions = new ();
    private readonly string _enumName;

    /// <summary>
    /// An enum named <paramref name="enumName"/>. Prefer
    /// <see cref="CSharpFileDefinition.AddEnum"/>, which builds one and attaches it to a file.
    /// </summary>
    public EnumDefinition(string enumName)
    {
        _enumName = enumName;
    }

    /// <summary>
    /// The underlying type: <c>public enum Level : byte</c>. <c>int</c> when left unset, which is
    /// what C# defaults to.
    /// </summary>
    /// <remarks>
    /// Worth setting where the numbers are part of a wire format or match a native definition,
    /// because the default is a silent four bytes per value.
    /// </remarks>
    public ITypeDefinition? BaseType { get; set; }

    /// <summary>
    /// Adds <c>[Flags]</c>, returning the enum so the call chains.
    /// </summary>
    /// <remarks>
    /// Shorthand for <c>AddAttribute(typeof(FlagsAttribute))</c>. It marks the enum; assigning the
    /// powers of two is still the caller's, through
    /// <see cref="AddValue(string, object)"/>.
    /// </remarks>
    public EnumDefinition AddFlags()
    {
        AddAttribute(TypeDefinition.Get(typeof(FlagsAttribute)));

        return this;
    }

    /// <summary>
    /// A member with no explicit value, numbered by the compiler: <c>Low,</c>.
    /// </summary>
    /// <remarks>
    /// Returns the member, which is where its <see cref="BaseOutputComponent.Comment"/> and any
    /// attributes go. Use <see cref="AddValue(string, object)"/> wherever the number is part of the
    /// contract - a flags enum, or anything serialized by value.
    /// </remarks>
    public EnumValueDefinition AddValue(string enumValueName)
    {
        var enumValueDefinition = new EnumValueDefinition(enumValueName);

        _enumValueDefinitions.Add(enumValueDefinition);

        return enumValueDefinition;
    }

    /// <summary>
    /// A member with an explicit value: <c>High = 10,</c>.
    /// </summary>
    /// <remarks>
    /// <paramref name="value"/> goes through the same literal formatter every other value does, so
    /// it is written as the C# that denotes it rather than as <c>ToString()</c>.
    /// </remarks>
    public EnumValueDefinition AddValue(string enumValueName, object value)
    {
        var enumValueDefinition = AddValue(enumValueName);

        enumValueDefinition.Value = value;

        return enumValueDefinition;
    }

    /// <summary>
    /// An enum inherits <see cref="BaseOutputComponent.Comment"/> like every other declaration, and
    /// used to be one of the two that never wrote it - so setting one compiled, read as documented,
    /// and emitted nothing.
    /// </summary>
    protected override void WriteComment(IOutputContext outputContext)
    {
        DocumentationComment.WriteSummary(outputContext.WriteIndentedLine, Comment);
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        WriteEnumSignature(outputContext);

        outputContext.OpenScope();

        foreach (var enumValueDefinition in _enumValueDefinitions)
        {
            enumValueDefinition.WriteOutput(outputContext);
        }

        outputContext.CloseScope();
    }

    private void WriteEnumSignature(IOutputContext outputContext)
    {
        var modifier =  GetAccessModifier("public");

        outputContext.WriteIndent();
        outputContext.Write($"{modifier} enum {CSharpIdentifier.Escape(_enumName)}");

        if (BaseType != null)
        {
            outputContext.Write(" : ");
            outputContext.Write(BaseType);
        }

        outputContext.WriteLine();
    }

    /// <summary>The declared name, escaped with <c>@</c> if it is a keyword.</summary>
    public string Name => _enumName;
}