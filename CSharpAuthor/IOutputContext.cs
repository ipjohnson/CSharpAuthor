using System;
using System.Collections.Generic;

namespace CSharpAuthor;

public interface IOutputContext
{
    string SingleIndent { get; }

    string IndentString { get; }

    void IncrementIndent();

    void DecrementIndent();

    void Write(string text);

    void Write(ITypeDefinition typeDefinition);

    void WriteLine();

    void WriteLine(string text);

    void WriteSpace();

    void WriteIndent(string text = "");

    void WriteIndentedLine(string text);

    string Output();

    void OpenScope();

    void CloseScope();

    void AddImportNamespace(string ns);

    void AddImportNamespace(ITypeDefinition typeDefinition);

    void AddImportNamespaces(IEnumerable<string> namespaces);

    void AddImportNamespaces(IEnumerable<ITypeDefinition> typeDefinition);

    void GenerateUsingStatements();
}