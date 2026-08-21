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

    /// <summary>
    /// Asks for a namespace by name.
    /// </summary>
    /// <remarks>
    /// For the thing qualification cannot express: an extension method is reached through a
    /// <c>using</c> and no other way. A namespace that exists because of a <em>type</em> is not
    /// asked for at all - it is derived from the types the file wrote, which is why the overloads
    /// that took one are no longer here. A writer that declares the namespace of a type it is about
    /// to write can get it wrong, and did: it declared them whatever the output mode was, so a file
    /// that qualified every name still carried a directive it did not need, and a name that carried
    /// no namespace of its own resolved because of it.
    /// </remarks>
    void AddImportNamespace(string ns);

    void AddImportNamespaces(IEnumerable<string> namespaces);

    void GenerateUsingStatements();
    
    char? LastCharacter { get; }
    
    OutputContextOptions Options { get; }
}