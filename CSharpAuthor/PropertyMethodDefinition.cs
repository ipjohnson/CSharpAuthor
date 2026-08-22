using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// One accessor of a <see cref="PropertyDefinition"/> - its <c>get</c> or its <c>set</c>.
/// </summary>
/// <remarks>
/// A <see cref="MethodDefinition"/> with no signature of its own: the property writes the
/// <c>get</c> or <c>set</c> keyword, and this writes what follows. Statements are added the same
/// way a method body's are, and an accessor with none of them is what makes the property an
/// auto-property.
/// </remarks>
public class PropertyMethodDefinition : MethodDefinition
{
    /// <summary>
    /// An accessor. Built by <see cref="PropertyDefinition"/>; there is no reason to construct one.
    /// </summary>
    public PropertyMethodDefinition() : base("")
    {

    }

    /// <summary>
    /// Writes the accessor as an expression body - <c>public string Name =&gt; _name;</c> - rather
    /// than as a braced block.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only for a getter, and only on a property whose <see cref="PropertyDefinition.Set"/> is
    /// null: an expression-bodied member is the whole property, so there is nowhere for a second
    /// accessor to go. It reads the <em>first</em> statement and ignores the rest, so an accessor
    /// with more than one is silently truncated.
    /// </para>
    /// <para>
    /// A trailing <c>;</c> is added if the statement does not already end in one, so the same
    /// statement text works in either form.
    /// </para>
    /// <para>
    /// Declared on <see cref="MethodDefinition"/> and inherited. It used to be redeclared here,
    /// which hid the base property - so once the method form gained one, setting it through a
    /// <see cref="MethodDefinition"/> reference and reading it through a
    /// <see cref="PropertyMethodDefinition"/> reference would have answered differently.
    /// </para>
    /// </remarks>

    /// <summary>
    /// Writes <c>init</c> in place of <c>set</c>: <c>public string Name { get; init; }</c>.
    /// </summary>
    /// <remarks>
    /// C# 9, and polyfillable - below it the keyword downlevels to <c>set</c> and a
    /// <c>// DOWNLEVEL:</c> comment says so, because a property that quietly stops being init-only
    /// is assignable from anywhere and nothing fails to compile. With no emit profile in force it
    /// is written as asked, which is what version 1 did.
    /// </remarks>
    public bool IsInit { get; set; }

    protected override void WriteMethodSignature(IOutputContext outputContext)
    {
        // don't write anything as it will be covered 
    }

    protected override void WriteMethodBody(IOutputContext outputContext)
    {
        if (LambdaSyntax)
        {
            outputContext.Write(" => ");

            // `Get.LambdaSyntax = true` together with `Get.Return(x)` is the natural pairing of two
            // documented APIs, and it emitted `=> return x;;` - the return keyword written into a
            // position C# does not allow, at the block's indent, followed by a stray terminator.
            // The unwrapping is shared with the method form so the two cannot drift apart.
            var statement = UnwrapExpressionBody(StatementList.First());

            statement.WriteOutput(outputContext);

            if (outputContext.LastCharacter != ';')
            {
                outputContext.Write(";");
            }
            
            outputContext.WriteLine();
        }
        else
        {
            outputContext.WriteLine();
            base.WriteMethodBody(outputContext);
        }
    }
}