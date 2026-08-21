using System;
using System.Collections.Generic;

namespace CSharpAuthor;

/// <summary>
/// What a component writes into: the recorder a file is built up in, and the source of the layout
/// and naming decisions it will be serialized with.
/// </summary>
/// <remarks>
/// <para>
/// Callers building a file rarely touch this directly - they build a tree and hand it to
/// <see cref="OutputContext"/>. It is the interface a <em>writer</em> sees: a custom
/// <see cref="IOutputComponent"/> implements its output against this rather than against a
/// <see cref="System.Text.StringBuilder"/>.
/// </para>
/// <para>
/// Nothing here produces text. Every call records a segment, and the segments become C# in
/// <see cref="Output"/>. That is what lets the <c>using</c> list be derived from the types written
/// and a contested short name be aliased - both need the whole file to be known first.
/// </para>
/// </remarks>
public interface IOutputContext
{
    /// <summary>One level of indent, as text - four spaces by default.</summary>
    string SingleIndent { get; }

    /// <summary>The indent at the current depth, as text.</summary>
    string IndentString { get; }

    /// <summary>
    /// Goes one level deeper without opening a brace - for a construct that indents its body and
    /// does not brace it, such as a <c>switch</c> arm.
    /// </summary>
    /// <remarks>
    /// Use <see cref="OpenScope"/> where a brace belongs: it also honours
    /// <see cref="OutputContextOptions.BraceStyle"/>, which this cannot, having no brace to place.
    /// </remarks>
    void IncrementIndent();

    /// <summary>Comes back out one level. Pairs with <see cref="IncrementIndent"/>.</summary>
    void DecrementIndent();

    /// <summary>
    /// Records literal text, with no indent and no line break around it.
    /// </summary>
    /// <remarks>
    /// Text written here is final - it cannot be qualified, aliased or counted for the
    /// <c>using</c> list. Anything that names a type should go through
    /// <see cref="Write(ITypeDefinition)"/> instead.
    /// </remarks>
    void Write(string text);

    /// <summary>
    /// Records a type reference, unrendered.
    /// </summary>
    /// <remarks>
    /// The call that makes the whole scheme work: the type is written out at serialization with
    /// whatever <see cref="OutputContextOptions.TypeOutputMode"/> the file settled on, aliased if
    /// its short name is contested, and its namespace is counted towards the <c>using</c> list.
    /// Passing <c>typeDefinition.GetShortName()</c> to <see cref="Write(string)"/> instead gives up
    /// all three.
    /// </remarks>
    void Write(ITypeDefinition typeDefinition);

    /// <summary>A line break.</summary>
    void WriteLine();

    /// <summary>Text followed by a line break, with no indent in front of it.</summary>
    void WriteLine(string text);

    /// <summary>A single space.</summary>
    void WriteSpace();

    /// <summary>
    /// The current indent, then <paramref name="text"/>, with no line break. The start of a line
    /// that something else will finish.
    /// </summary>
    void WriteIndent(string text = "");

    /// <summary>The current indent, then <paramref name="text"/>, then a line break.</summary>
    void WriteIndentedLine(string text);

    /// <summary>
    /// Serializes everything recorded so far into C#. See <see cref="OutputContext.Output"/>.
    /// </summary>
    string Output();

    /// <summary>
    /// Opens a <c>{</c> and indents.
    /// </summary>
    /// <remarks>
    /// Recorded as a marker rather than as the character, so
    /// <see cref="OutputContextOptions.BraceStyle"/> decides where it lands when the file is
    /// serialized - a file already written can be restyled.
    /// </remarks>
    void OpenScope();

    /// <summary>Outdents and closes with <c>}</c>. Pairs with <see cref="OpenScope"/>.</summary>
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

    /// <inheritdoc cref="AddImportNamespace(string)" />
    /// <remarks>The overload for several at once. Duplicates are harmless.</remarks>
    void AddImportNamespaces(IEnumerable<string> namespaces);

    /// <summary>
    /// Says that this file should carry a <c>using</c> block, and where it goes.
    /// </summary>
    /// <remarks>
    /// Called once by <see cref="CSharpFileDefinition"/> after everything else has been written.
    /// A fragment written without it - a component serialized on its own - gets no directives,
    /// which is correct: it is going to be embedded in a file that has its own.
    /// </remarks>
    void GenerateUsingStatements();
    
    /// <summary>
    /// The last character recorded, or null if nothing has been. For a writer deciding whether to
    /// add a separator that may already be there - an expression-bodied accessor checking for its
    /// own <c>;</c>.
    /// </summary>
    char? LastCharacter { get; }
    
    /// <summary>
    /// The layout and naming decisions for this file. Read by writers that vary their output -
    /// <see cref="OutputContextOptions.BreakInvokeLines"/> and
    /// <see cref="OutputContextOptions.GenerateDocumentation"/> are both consulted while writing.
    /// </summary>
    OutputContextOptions Options { get; }
}