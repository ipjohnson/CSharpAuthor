#nullable enable

namespace CSharpAuthor.Syntax;

/// <summary>
/// What a token *is*, for spacing purposes. The grammar encodes token order; it does not
/// encode spacing. The generator assigns every emitted token a role, derived structurally
/// from the grammar (the token's kind, its position in the node, and the node's category),
/// and <see cref="SyntaxWriter"/> turns (previous role, next role) into whitespace.
/// </summary>
/// <remarks>
/// No rule here is keyed on an individual node name. Every role assignment in
/// <c>gen_all.py</c> is a category rule, so adding a C# version cannot invalidate them.
/// </remarks>
#if CSHARPAUTHOR_PUBLIC_SYNTAX
public
#endif
enum TokenRole
{
    /// <summary>Nothing has been written on this line yet.</summary>
    None = 0,

    /// <summary>A keyword: <c>class</c>, <c>if</c>, <c>return</c>. Separates from its neighbours.</summary>
    Word,

    /// <summary>A keyword that binds its parenthesis tight: <c>typeof(</c>, <c>nameof(</c>, <c>default(</c>.</summary>
    FnWord,

    /// <summary>An identifier. Escaped with <c>@</c> when it collides with a keyword.</summary>
    Name,

    /// <summary>
    /// A keyword that names a type: <c>int</c>, <c>string</c>, <c>void</c>, <c>ref</c>.
    /// Binds to <c>[</c> and <c>?</c> the way an identifier does - <c>int[]</c>, <c>int?</c> -
    /// but is never escaped, because it is a keyword and means to be one.
    /// </summary>
    TypeWord,

    /// <summary>A literal: already-quoted string, number, char.</summary>
    Literal,

    OpenParen,
    CloseParen,

    /// <summary>The <c>(</c> of a cast. Same spacing as <see cref="OpenParen"/>.</summary>
    OpenParenCast,

    /// <summary>The <c>)</c> of a cast: binds tight to the operand, so <c>(int)x</c>.</summary>
    CloseParenCast,

    /// <summary>Array rank, indexer, collection expression: binds tight after a name or type.</summary>
    OpenBracket,
    CloseBracket,

    /// <summary>An attribute list's bracket. Owns its line in member and statement position.</summary>
    OpenBracketAttr,
    CloseBracketAttr,

    /// <summary>Generic angle brackets. Always tight - comparison operators arrive as <see cref="Operator"/>.</summary>
    OpenAngle,
    CloseAngle,

    /// <summary>Allman block brace. Goes through the context's scope markers.</summary>
    OpenBrace,
    CloseBrace,

    /// <summary>Initializer / pattern / <c>with</c> brace. Stays on the line, spaced.</summary>
    OpenBraceInline,
    CloseBraceInline,

    Comma,

    /// <summary>A semicolon in the middle of a node - the two in <c>for(;;)</c>. Space after, no break.</summary>
    SemiSeparator,

    /// <summary>A semicolon that is the node's last token. Ends the line.</summary>
    SemiTerminator,

    /// <summary>
    /// A semicolon that closes a section header rather than a statement - the one in
    /// <c>namespace Acme;</c>. Ends the line and leaves a blank one after it, because what
    /// follows is a body, not the next statement.
    /// </summary>
    SemiSection,

    /// <summary>Member access: <c>.</c> <c>::</c> <c>-&gt;</c> <c>?.</c>. Tight both sides.</summary>
    Dot,

    /// <summary>Base list, constraint clause, constructor initializer, ternary. Spaced both sides.</summary>
    Colon,

    /// <summary>Named argument, attribute target, interpolation format. Tight before, space after.</summary>
    ColonTight,

    /// <summary>Switch label, statement label. Tight before, ends the line.</summary>
    ColonLine,

    /// <summary>Binary or assignment operator. Spaced both sides.</summary>
    Operator,

    /// <summary>Prefix operator: <c>!</c> <c>-</c> <c>++</c> <c>&amp;</c>. Space before, tight after.</summary>
    PrefixOperator,

    /// <summary>Postfix operator: <c>++</c> <c>--</c> <c>!</c>. Tight before, space after.</summary>
    PostfixOperator,

    /// <summary>Nullable type marker <c>int?</c>. Tight both sides.</summary>
    QuestionTight,

    /// <summary>A preprocessor directive. Owns its line and never takes indentation.</summary>
    Directive,

    /// <summary>Raw text of unknown shape - spacing is left exactly as handed over.</summary>
    Raw,
}

/// <summary>
/// How the elements of a grammar list are joined. Chosen by the generator from the list's
/// element type and the containing node's category - never from the node's name.
/// </summary>
#if CSHARPAUTHOR_PUBLIC_SYNTAX
public
#endif
enum ListStyle
{
    /// <summary>No separator at all - the token spacing rules do the work.</summary>
    None = 0,

    /// <summary>Comma plus a space. The default for every <c>SeparatedSyntaxList</c>.</summary>
    Comma,

    /// <summary>Comma then a line break. Enum members.</summary>
    CommaLine,

    /// <summary>A line break between elements. Statements, accessors, catch clauses, switch sections.</summary>
    Line,

    /// <summary>A blank line between elements. Member declarations.</summary>
    Blank,

    /// <summary>A line break after every element, including the last. Attribute lists on members.</summary>
    LineEach,

    /// <summary>A line break after every element and one blank line after the list. Usings.</summary>
    UsingBlock,

    /// <summary>Every element on its own line at one extra indent. Type parameter constraint clauses.</summary>
    IndentedLines,
}
