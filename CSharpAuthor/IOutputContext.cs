namespace CSharpAuthor
{
    public interface IOutputContext
    {
        string IndentString { get; }

        void IncrementIndent();

        void DecrementIndent();

        void Write(string text);

        void WriteLine() => Write(Environment.NewLine);

        void WriteLine(string text);

        void WriteSpace() => Write(" ");

        void WriteIndent() => Write(IndentString);

        void WriteIndentedLine(string text);

        string Output();

        void OpenScope();

        void CloseScope();

        void AddImportNamespace(string ns);

        void AddImportNamespace(TypeDefinition typeDefinition) => AddImportNamespace(typeDefinition.Namespace);

        void GenerateUsingStatements();
    }
}
