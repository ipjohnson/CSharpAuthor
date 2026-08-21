using System.Collections.Generic;

namespace CSharpAuthor;

/// <summary>
/// <c>#if</c>, its <c>#elif</c> and <c>#else</c> branches, and the <c>#endif</c> that closes them.
/// </summary>
/// <remarks>
/// <para>
/// A container rather than a set of markers, for the same reason as
/// <see cref="RegionComponent"/>: the closing directive is structural, so it cannot be forgotten
/// or misplaced.
/// </para>
/// <para>
/// <strong>A <c>using</c> needed only by an excluded branch is still written.</strong> Imports are
/// derived from every type written anywhere in the file, and the branches are all written - only
/// the compiler decides which survives. That errs the safe way: a <c>using</c> that turns out to be
/// unneeded is CS8019 at worst, an unused-directive hint, whereas a missing one does not compile.
/// </para>
/// </remarks>
public class ConditionalDirectiveComponent : BaseOutputComponent
{
    private readonly List<Branch> _branches = new();
    private Branch? _elseBranch;

    /// <param name="condition">
    /// The condition after <c>#if</c>, written as given - <c>NET8_0_OR_GREATER</c>,
    /// <c>DEBUG &amp;&amp; !NO_LOGGING</c>. It is a preprocessor expression, not a C# one, so it is
    /// text rather than a component.
    /// </param>
    public ConditionalDirectiveComponent(string condition)
    {
        _branches.Add(new Branch(condition));
    }

    /// <summary>The <c>#if</c> branch, to add components to.</summary>
    public Branch If => _branches[0];

    /// <summary>Adds an <c>#elif</c> branch and returns it. Branches are written in order.</summary>
    public Branch ElseIf(string condition)
    {
        var branch = new Branch(condition);

        _branches.Add(branch);

        return branch;
    }

    /// <summary>
    /// The <c>#else</c> branch, written last whichever order it was asked for in. Asking twice
    /// returns the same branch rather than discarding the first.
    /// </summary>
    public Branch Else()
    {
        return _elseBranch ??= new Branch(condition: null);
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        for (var i = 0; i < _branches.Count; i++)
        {
            outputContext.WriteIndentedLine(
                (i == 0 ? "#if " : "#elif ") + _branches[i].Condition);

            _branches[i].WriteComponents(outputContext);
        }

        if (_elseBranch != null)
        {
            outputContext.WriteIndentedLine("#else");

            _elseBranch.WriteComponents(outputContext);
        }

        outputContext.WriteIndentedLine("#endif");
    }

    /// <inheritdoc cref="RegionComponent.ProcessLeadingTraits"/>
    protected override void ProcessLeadingTraits(IOutputContext outputContext)
    {
    }

    /// <inheritdoc cref="RegionComponent.ProcessLeadingTraits"/>
    protected override void ProcessTrailingTraits(IOutputContext outputContext)
    {
    }

    /// <summary>
    /// One arm of a conditional directive, and what it holds.
    /// </summary>
    public class Branch
    {
        private readonly List<IOutputComponent> _components = new();

        internal Branch(string? condition)
        {
            Condition = condition;
        }

        /// <summary>The branch's condition, or null for the <c>#else</c> arm.</summary>
        public string? Condition { get; }

        /// <summary>Puts a component in this branch. Returns it, typed, so it stays usable.</summary>
        public T Add<T>(T component) where T : IOutputComponent
        {
            _components.Add(component);

            return component;
        }

        internal void WriteComponents(IOutputContext outputContext)
        {
            foreach (var component in _components)
            {
                component.WriteOutput(outputContext);
            }
        }
    }
}
