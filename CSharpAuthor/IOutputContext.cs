using System;
using System.Collections.Generic;

namespace CSharpAuthor
{
    public interface IOutputContext
    {
        string IndentString { get; }

        void IncrementIndent();

        void DecrementIndent();

        void Write(string text);

        void Write(ITypeDefinition typeDefinition);

        void WriteLine();

        void WriteLine(string text);

        void WriteSpace();

        void WriteIndent();

        void WriteIndentedLine(string text);

        string Output();

        void OpenScope();

        void CloseScope();

        void AddImportNamespace(string ns);

        void AddImportNamespace(ITypeDefinition typeDefinition);

        void AddImportNamespace(IEnumerable<ITypeDefinition> typeDefinition);

        void GenerateUsingStatements();
    }
}
