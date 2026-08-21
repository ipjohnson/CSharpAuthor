using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class InterfaceMethodDefinition : MethodDefinition
{
    public InterfaceMethodDefinition(string name) : base(name)
    {

    }

    /// <summary>
    /// Whether the member is declared <c>static abstract</c>, for an interface used as a
    /// constraint.
    /// </summary>
    /// <remarks>
    /// C# 11, and one of the features with no downlevel at all: dropping the keywords gives an
    /// instance member, which is a different interface. Asking for it below C# 11 is a capability
    /// violation, not a formatting decision.
    /// </remarks>
    public bool IsStaticAbstract { get; set; }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        var session = outputContext.EmitSession();

        // Both checks happen before anything is written, so a `#error` directive lands on a line
        // of its own rather than in the middle of a signature.
        if (StatementCount > 0)
        {
            // A member with a body is a default interface member.
            session.Require(LanguageFeature.DefaultInterfaceMembers, outputContext, Name);
        }

        if (IsStaticAbstract)
        {
            session.Require(LanguageFeature.StaticAbstractInterfaceMembers, outputContext, Name);
        }

        base.WriteComponentOutput(outputContext);
    }

    protected override void WriteMethodBody(IOutputContext outputContext)
    {
        if (StatementCount > 0)
        {
            base.WriteMethodBody(outputContext);
        }
    }

    protected override void WriteEndOfMethodSignature(IOutputContext outputContext)
    {
        if (StatementCount == 0)
        {
            outputContext.Write(";");
        }

        outputContext.WriteLine();
    }
        

    protected override void WriteAccessModifier(IOutputContext outputContext)
    {
        outputContext.WriteIndent();

        if (IsStaticAbstract && outputContext.EmitProfile().Supports(LanguageFeature.StaticAbstractInterfaceMembers))
        {
            outputContext.Write("static abstract ");
        }
    }
}