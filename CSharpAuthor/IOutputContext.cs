using System;

namespace CSharpAuthor
{
    public interface IOutputContext
    {
        string IndentString { get; }

        void IncrementIndent();

        void DecrementIndent();

        void Write(string text);

        void WriteLine();

        void WriteLine(string text);

        void WriteSpace();

        void WriteIndent();

        void WriteIndentedLine(string text);

        string Output();

        void OpenScope();

        void CloseScope();

        void AddImportNamespace(string ns);

        void AddImportNamespace(TypeDefinition typeDefinition);

        void GenerateUsingStatements();
    }
}
