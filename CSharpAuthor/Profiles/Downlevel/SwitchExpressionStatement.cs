using System;
using System.Collections.Generic;

namespace CSharpAuthor.Profiles;

/// <summary>
/// A value chosen from a set of tests: <c>x switch { 1 =&gt; "one", _ =&gt; null }</c> on C# 8, and
/// the conditional chain that means the same thing before it.
/// </summary>
/// <remarks>
/// The downlevel is only correct when the arms are equality tests against constants, because that
/// is all <c>?:</c> can express. Anything richer - a type pattern, a property pattern, a relational
/// one - has no conditional form, and saying so is <see cref="ArmsAreEqualityTests"/>. The writer
/// cannot check it, so it asks rather than assumes; assuming is how a switch on types becomes a
/// chain of reference comparisons that compiles and is wrong.
/// </remarks>
public class SwitchExpressionStatement : BaseOutputComponent
{
    private readonly IOutputComponent _input;
    private readonly List<KeyValuePair<IOutputComponent, IOutputComponent>> _arms =
        new List<KeyValuePair<IOutputComponent, IOutputComponent>>();

    /// <summary>Switches on <paramref name="input"/>.</summary>
    public SwitchExpressionStatement(IOutputComponent input)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
    }

    /// <inheritdoc cref="SwitchExpressionStatement(IOutputComponent)" />
    public SwitchExpressionStatement(string input)
        : this(new CodeOutputComponent(input) { Indented = false })
    {
    }

    /// <summary>The <c>_ =&gt;</c> arm. Required by the conditional downlevel.</summary>
    public IOutputComponent? DefaultResult { get; set; }

    /// <summary>
    /// Whether every arm is an equality test against a constant, and so has a conditional form.
    /// </summary>
    public bool ArmsAreEqualityTests { get; set; } = true;

    /// <summary>What is being switched on, named in any diagnostic it produces.</summary>
    public string? Context { get; set; }

    /// <summary>The arms, in order.</summary>
    public IReadOnlyList<KeyValuePair<IOutputComponent, IOutputComponent>> Arms => _arms;

    /// <summary>Adds an arm.</summary>
    public SwitchExpressionStatement AddArm(IOutputComponent test, IOutputComponent result)
    {
        _arms.Add(new KeyValuePair<IOutputComponent, IOutputComponent>(test, result));

        return this;
    }

    /// <inheritdoc cref="AddArm(IOutputComponent,IOutputComponent)" />
    public SwitchExpressionStatement AddArm(string test, string result) =>
        AddArm(
            new CodeOutputComponent(test) { Indented = false },
            new CodeOutputComponent(result) { Indented = false });

    /// <summary>Sets the <c>_ =&gt;</c> arm.</summary>
    public SwitchExpressionStatement Otherwise(string result)
    {
        DefaultResult = new CodeOutputComponent(result) { Indented = false };

        return this;
    }

    /// <inheritdoc />
    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        var session = outputContext.EmitSession();

        if (session.MayEmit(LanguageFeature.SwitchExpressions, outputContext, Context))
        {
            _input.WriteOutput(outputContext);
            outputContext.Write(" switch { ");

            foreach (var arm in _arms)
            {
                arm.Key.WriteOutput(outputContext);
                outputContext.Write(" => ");
                arm.Value.WriteOutput(outputContext);
                outputContext.Write(", ");
            }

            outputContext.Write("_ => ");

            if (DefaultResult == null)
            {
                outputContext.Write("throw new global::System.InvalidOperationException()");
            }
            else
            {
                DefaultResult.WriteOutput(outputContext);
            }

            outputContext.Write(" }");

            return;
        }

        if (!ArmsAreEqualityTests || DefaultResult == null)
        {
            session.NoDownlevelAvailable(
                LanguageFeature.SwitchExpressions,
                outputContext,
                Context,
                ArmsAreEqualityTests
                    ? "a conditional chain has no way to express a switch with no default arm."
                    : "a conditional chain can only express arms that are equality tests against constants.");

            return;
        }

        // Right-associative, so the nesting needs no parentheses:
        //   x == 1 ? "one" : x == 2 ? "two" : null
        foreach (var arm in _arms)
        {
            _input.WriteOutput(outputContext);
            outputContext.Write(" == ");
            arm.Key.WriteOutput(outputContext);
            outputContext.Write(" ? ");
            arm.Value.WriteOutput(outputContext);
            outputContext.Write(" : ");
        }

        DefaultResult.WriteOutput(outputContext);
    }
}
