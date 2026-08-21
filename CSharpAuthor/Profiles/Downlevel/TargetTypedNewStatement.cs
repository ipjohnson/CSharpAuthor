using System.Collections.Generic;
using System.Linq;

namespace CSharpAuthor.Profiles;

/// <summary>
/// An object being constructed: <c>new()</c> where the type is already obvious and the target
/// allows it, <c>new StringBuilder()</c> otherwise.
/// </summary>
/// <remarks>
/// The type is always carried, even when it is not written - dropping it would mean the downlevel
/// rendering had nothing to write, which is exactly the trap that makes text impossible to
/// downlevel and a tree easy.
/// </remarks>
#if CSHARPAUTHOR_PUBLIC_API
public
#endif
class TargetTypedNewStatement : BaseOutputComponent
{
    private readonly ITypeDefinition _typeDefinition;
    private readonly IReadOnlyList<IOutputComponent> _arguments;

    /// <summary>Constructs <paramref name="typeDefinition"/>.</summary>
    public TargetTypedNewStatement(ITypeDefinition typeDefinition, params IOutputComponent[] arguments)
        : this(typeDefinition, (IReadOnlyList<IOutputComponent>)arguments)
    {
    }

    /// <inheritdoc cref="TargetTypedNewStatement(ITypeDefinition,IOutputComponent[])" />
    public TargetTypedNewStatement(ITypeDefinition typeDefinition, IReadOnlyList<IOutputComponent> arguments)
    {
        _typeDefinition = typeDefinition;
        _arguments = arguments;
    }

    /// <summary>Constructs a type from arguments of any kind.</summary>
    public static TargetTypedNewStatement Of(ITypeDefinition typeDefinition, params object[] arguments) =>
        new TargetTypedNewStatement(typeDefinition, CodeOutputComponent.GetAll(arguments).ToList());

    /// <summary>What is being constructed, named in any diagnostic it produces.</summary>
    public string? Context { get; set; }

    /// <inheritdoc />
    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        var session = outputContext.EmitSession();

        outputContext.Write("new");

        if (!session.MayEmit(LanguageFeature.TargetTypedNew, outputContext, Context))
        {
            outputContext.WriteSpace();

            // Through Write(ITypeDefinition), never AddImportNamespace: the using follows from the
            // type having been written, and in the target-typed form nothing is written and no
            // using is claimed.
            outputContext.Write(_typeDefinition);
        }

        outputContext.Write("(");
        _arguments.OutputCommaSeparatedList(outputContext);
        outputContext.Write(")");
    }
}
