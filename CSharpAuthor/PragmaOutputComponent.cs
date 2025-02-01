namespace CSharpAuthor;

public class PragmaOutputComponent : BaseOutputComponent
{
    private bool _enable;
    private string[] _pragma;
    
    public PragmaOutputComponent(bool enable, params string[] pragma)
    {
        _pragma = pragma;
        _enable = enable;
    }
    
    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        var enableString = _enable ? "restore" : "disable";
        outputContext.WriteIndent($"#pragma warning {enableString} ");
        _pragma.OutputCommaSeparatedList(outputContext, (context, s) => context.Write(s));
        outputContext.WriteLine();
    }

    protected override void ProcessLeadingTraits(IOutputContext outputContext)
    {
        // do nothing 
    }
    protected override void ProcessTrailingTraits(IOutputContext outputContext)
    {
        // do nothing
    }
}