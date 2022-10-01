namespace CSharpAuthor;

public interface IOutputComponent
{
    void AddUsingNamespace(string ns);

    void WriteOutput(IOutputContext outputContext);
}