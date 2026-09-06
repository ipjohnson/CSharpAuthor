using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// One member of an <see cref="EnumDefinition"/>.
/// </summary>
public class EnumValueDefinition : BaseOutputComponent
{
    private readonly string _enumValueName;

    /// <summary>
    /// A member named <paramref name="enumValueName"/>. Prefer
    /// <see cref="EnumDefinition.AddValue(string)"/>, which builds one and attaches it.
    /// </summary>
    public EnumValueDefinition(string enumValueName)
    {
        _enumValueName = enumValueName;
    }

    /// <summary>
    /// The explicit value, or null to let the compiler number it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null means "no <c>= n</c>", not the value zero - a member that should be zero has to say so.
    /// </para>
    /// <para>
    /// A literal or an <see cref="IOutputComponent"/>, so a computed value is reachable:
    /// <c>value.Value = Ex.Shift(1, i)</c> writes <c>= 1 &lt;&lt; i</c>, which is how a flag enum
    /// is built.
    /// </para>
    /// </remarks>
    public object? Value { get; set; }

    /// <summary>
    /// The member's own documentation, which is where a specification's description of a single
    /// enum value belongs. Silently dropped before this existed.
    /// </summary>
    protected override void WriteComment(IOutputContext outputContext)
    {
        DocumentationComment.WriteSummary(outputContext.WriteIndentedLine, Comment);
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.WriteIndent();
        outputContext.Write(CSharpIdentifier.Escape(_enumValueName));

        if (Value != null)
        {
            outputContext.Write(" = ");

            // Through CodeOutputComponent.Get, the way every other value API in the library goes.
            // Formatting the object directly meant anything that was not a literal fell through to
            // its own ToString(), so a component arrived as its class name - `A =
            // CSharpAuthor.Expressions.Ex` - and a flag enum built from `1 << n` was unreachable,
            // silently. Get() passes an IOutputComponent through untouched and still routes a plain
            // literal to LiteralFormatter.
            CodeOutputComponent.Get(Value).WriteOutput(outputContext);
        }

        outputContext.WriteLine(",");
    }
}