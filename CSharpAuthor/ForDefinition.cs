namespace CSharpAuthor;

/// <summary>
/// A <c>for</c> loop.
/// </summary>
/// <remarks>
/// <para>
/// This was a class with an empty <c>WriteComponentOutput</c> and nothing that returned one, so a
/// caller who found it and constructed one got a loop that wrote nothing at all - the body
/// included. Anything needing an index had to fall back to <c>AddCode</c> and hand-write the
/// header, which puts the loop variable outside anything the library can see.
/// </para>
/// <para>
/// All three clauses are optional, because C# allows each to be empty and <c>for (;;)</c> is a
/// legal infinite loop.
/// </para>
/// </remarks>
public class ForDefinition : BaseBlockDefinition
{
    /// <summary>
    /// A loop with no clauses set - <c>for(; ; )</c> until
    /// <see cref="Initializer"/>, <see cref="Condition"/> and <see cref="Increment"/> are assigned.
    /// </summary>
    public ForDefinition()
    {
    }

    /// <summary>
    /// A loop with all three clauses given. Any of them may be null, because C# allows each to be
    /// empty. Prefer <see cref="BaseBlockDefinition.For(IOutputComponent, IOutputComponent, IOutputComponent)"/>,
    /// which builds one and attaches it to a block.
    /// </summary>
    public ForDefinition(
        IOutputComponent? initializer, IOutputComponent? condition, IOutputComponent? increment)
    {
        Initializer = initializer;
        Condition = condition;
        Increment = increment;
    }

    /// <summary>
    /// The counting loop, <c>for(var i = 0; i &lt; limit; i++)</c>, which is what a generator
    /// almost always wants.
    /// </summary>
    /// <remarks>
    /// <paramref name="startValue"/> and <paramref name="exclusiveLimit"/> take anything the rest
    /// of the library takes as a value - a literal, a component, another instance - so the limit
    /// can be an expression such as a collection's Count.
    /// </remarks>
    public ForDefinition(string variableName, object startValue, object exclusiveLimit)
    {
        Variable = new InstanceDefinition(variableName) { Indented = false };

        Initializer = new AppendStatement(
            "var " + CSharpIdentifier.Escape(variableName) + " = ",
            CodeOutputComponent.Get(startValue, false));

        // The clauses of a for header are already delimited by its semicolons, so the parentheses
        // LogicStatement adds by default would only be noise.
        Condition = new LogicStatement(" < ", Variable, CodeOutputComponent.Get(exclusiveLimit, false))
        {
            PrintParentheses = false
        };

        Increment = SyntaxHelpers.Increment(Variable);
    }

    /// <summary>
    /// The loop variable, when this was built as a counting loop. Usable as a value anywhere a
    /// component is taken, so the body can refer to it without repeating its name.
    /// </summary>
    public InstanceDefinition? Variable { get; }

    /// <summary>
    /// The first clause, run once before the loop.
    /// </summary>
    public IOutputComponent? Initializer { get; set; }

    /// <summary>
    /// The second clause, tested before each iteration. Omitted means loop forever.
    /// </summary>
    public IOutputComponent? Condition { get; set; }

    /// <summary>
    /// The third clause, run after each iteration.
    /// </summary>
    public IOutputComponent? Increment { get; set; }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.WriteIndent("for(");

        WriteClause(outputContext, Initializer);
        outputContext.Write("; ");

        WriteClause(outputContext, Condition);
        outputContext.Write("; ");

        WriteClause(outputContext, Increment);

        outputContext.WriteLine(")");

        WriteBlock(outputContext);
    }

    private static void WriteClause(IOutputContext outputContext, IOutputComponent? clause)
    {
        if (clause == null)
        {
            return;
        }

        // Every clause is part of the header line, whatever the component would do on its own.
        if (clause is BaseOutputComponent baseOutputComponent)
        {
            baseOutputComponent.Indented = false;
        }

        clause.WriteOutput(outputContext);
    }
}
