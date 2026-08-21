#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CSharpAuthor.Syntax;

/// <summary>
/// The spacing, line-breaking and blank-line policy for the generated grammar.
/// </summary>
/// <remarks>
/// <para>
/// The grammar encodes token <em>order</em>. It does not encode whitespace, and nothing in
/// Roslyn's <c>Syntax.xml</c> ever will. This class is the whitespace half, and it is the
/// only hand-written part of the emit path.
/// </para>
/// <para>
/// It works on a token stream, not on the tree: every generated node hands over a sequence
/// of (<see cref="TokenRole"/>, text) pairs, and the decision to emit a space or a line
/// break is made from the pair (previous role, next role). No rule is keyed on a node name,
/// so a new C# version adds nodes without invalidating any of this.
/// </para>
/// <para>
/// State lives beside the <see cref="IOutputContext"/> rather than on it, so the policy
/// composes with any context implementation and with V1 components that write to the same
/// context directly. Everything it emits goes through <see cref="IOutputContext"/> - it
/// never builds a string, so a type handed to <see cref="Type"/> stays unrendered until the
/// context serialises.
/// </para>
/// </remarks>
#if CSHARPAUTHOR_PUBLIC_SYNTAX
public
#endif
sealed class SyntaxWriter
{
    private static readonly ConditionalWeakTable<IOutputContext, SyntaxWriter> Writers = new();

    private readonly IOutputContext _context;
    private TokenRole _previous;
    private int _pendingBreaks;
    private int _emittedBreaks = 1;

    private SyntaxWriter(IOutputContext context) => _context = context;

    /// <summary>
    /// The writer for this context, created on first use. Every node in a tree shares one,
    /// which is what lets the spacing rules see across node boundaries.
    /// </summary>
    public static SyntaxWriter For(IOutputContext context)
    {
        if (Writers.TryGetValue(context, out var existing))
        {
            return existing;
        }

        var created = new SyntaxWriter(context);
        Writers.Add(context, created);
        return created;
    }

    /// <summary>The context this writer feeds. Handed to child nodes unchanged.</summary>
    public IOutputContext Context => _context;

    // -----------------------------------------------------------------------------------
    // Line state
    //
    // R14: a line break is *requested*, never written on the spot. A request says "at least
    // this many line breaks separate the previous token from the next one", so consecutive
    // requests collapse instead of stacking, a request with nothing after it is dropped, and
    // the indent is written lazily by the first token on the line. That is what makes
    // trailing whitespace and runaway blank lines structurally impossible rather than merely
    // unlikely - and it is what lets the context's own OpenScope/CloseScope, which write a
    // newline of their own, cooperate with a pending blank line instead of doubling it.
    // -----------------------------------------------------------------------------------

    /// <summary>
    /// Reconcile with the context before touching it. A V1 component sharing this context
    /// writes without going through the writer, so the count of line breaks currently at the
    /// end of the output is re-derived from the context rather than trusted.
    /// </summary>
    private void Sync()
    {
        var last = _context.LastCharacter;

        if (last == null)
        {
            // Nothing written yet: the output behaves as though it just began a line, so the
            // first token neither breaks nor indents into empty space.
            if (_emittedBreaks == 0)
            {
                _emittedBreaks = 1;
            }
        }
        else if (last == '\n' || last == '\r')
        {
            if (_emittedBreaks == 0)
            {
                _emittedBreaks = 1;
            }
        }
        else
        {
            _emittedBreaks = 0;
        }
    }

    /// <summary>Ask for at least <paramref name="count"/> line breaks before the next token.</summary>
    public void Break(int count = 1)
    {
        if (count > _pendingBreaks)
        {
            _pendingBreaks = count;
        }
    }

    /// <summary>Ask for one blank line before the next token.</summary>
    public void BlankLine() => Break(2);

    /// <summary>
    /// Write out however many line breaks are still owed - never more than were asked for,
    /// and never any that the context has already written.
    /// </summary>
    private void SettleBreaks(int atLeast)
    {
        Sync();

        var wanted = _pendingBreaks > atLeast ? _pendingBreaks : atLeast;
        _pendingBreaks = 0;

        for (var i = _emittedBreaks; i < wanted; i++)
        {
            _context.WriteLine();
            _emittedBreaks++;
        }

        if (_emittedBreaks > 0)
        {
            _previous = TokenRole.None;
        }
    }

    private void OpenLine()
    {
        SettleBreaks(0);

        if (_emittedBreaks > 0)
        {
            // R13: indentation comes from the context, never from a depth counter here.
            _context.WriteIndent();
            _emittedBreaks = 0;
            _previous = TokenRole.None;
        }
    }

    // -----------------------------------------------------------------------------------
    // Tokens
    // -----------------------------------------------------------------------------------

    /// <summary>Write one token with the role the generator assigned to it.</summary>
    public void Token(TokenRole role, string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        switch (role)
        {
            case TokenRole.OpenBrace:
                OpenBrace();
                return;
            case TokenRole.CloseBrace:
                CloseBrace();
                return;
            case TokenRole.Directive:
                // R16: a directive owns its line and never takes indentation.
                SettleBreaks(1);
                _context.Write(text!);
                _emittedBreaks = 0;
                _previous = TokenRole.Directive;
                return;
        }

        OpenLine();

        if (_previous != TokenRole.None && NeedsSpace(_previous, role))
        {
            _context.WriteSpace();
        }

        _context.Write(role == TokenRole.Name ? Escape(text!) : text!);
        _previous = role;

        switch (role)
        {
            case TokenRole.SemiTerminator:
            case TokenRole.ColonLine:
                Break();
                break;
            case TokenRole.SemiSection:
                BlankLine();
                break;
        }
    }

    /// <summary>Write an identifier, escaping it with <c>@</c> if it collides with a keyword.</summary>
    public void Name(string? text) => Token(TokenRole.Name, text);

    /// <summary>
    /// Write a type. The deferral point: the type is handed to the context unrendered, so
    /// short-name/global qualification and collision aliasing are still decided at
    /// serialisation time.
    /// </summary>
    public void Type(TypeRef type)
    {
        if (type.IsEmpty)
        {
            return;
        }

        if (type.Node != null)
        {
            // A type built out of grammar nodes writes its own tokens, and each of those
            // already claims whatever space it needs. Claiming one here as well is how the
            // prototype produced `new   int[]`.
            Node(type.Node);
            return;
        }

        OpenLine();

        if (_previous != TokenRole.None && NeedsSpace(_previous, TokenRole.Name))
        {
            _context.WriteSpace();
        }

        type.Write(this);
        _emittedBreaks = 0;

        // A rendered type ends in an identifier, `>`, `]` or `?`; all four behave as a name
        // for the token that follows, which is what makes `List<int>[] x` and `Foo(x)` work.
        _previous = TokenRole.Name;
    }

    /// <summary>Write a child node, if present.</summary>
    public void Node(ISyntax? node) => node?.WriteOutput(_context);

    // -----------------------------------------------------------------------------------
    // Braces - R9, R13
    // -----------------------------------------------------------------------------------

    /// <summary>
    /// Allman block brace. Goes through the context's scope markers so the context owns
    /// indentation; this class never counts depth.
    /// </summary>
    private void OpenBrace()
    {
        SettleBreaks(1);
        _context.OpenScope();
        _emittedBreaks = 1;
        _previous = TokenRole.None;
    }

    private void CloseBrace()
    {
        // A blank line asked for by the last member does not belong in front of the brace
        // that closes the scope, so the request is dropped rather than settled.
        _pendingBreaks = 0;
        SettleBreaks(1);
        _context.CloseScope();
        _emittedBreaks = 1;
        _previous = TokenRole.None;
    }

    // -----------------------------------------------------------------------------------
    // Lists - R11, R12
    // -----------------------------------------------------------------------------------

    /// <summary>Write a grammar list with the style the generator derived from its element type.</summary>
    public void List<T>(List<T> items, ListStyle style) where T : ISyntax
    {
        if (items.Count == 0)
        {
            return;
        }

        if (style == ListStyle.IndentedLines)
        {
            _context.IncrementIndent();
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                Between(style);
            }
            else if (style == ListStyle.IndentedLines)
            {
                Break();
            }

            items[i].WriteOutput(_context);

            if (style == ListStyle.LineEach || style == ListStyle.UsingBlock)
            {
                Break();
            }
        }

        if (style == ListStyle.IndentedLines)
        {
            _context.DecrementIndent();
        }
        else if (style == ListStyle.UsingBlock)
        {
            BlankLine();
        }
    }

    /// <summary>Write a <c>SyntaxList&lt;SyntaxToken&gt;</c> - modifiers, and nothing else that matters.</summary>
    public void Tokens(List<string> items, TokenRole role)
    {
        for (var i = 0; i < items.Count; i++)
        {
            Token(role, items[i]);
        }
    }

    private void Between(ListStyle style)
    {
        switch (style)
        {
            case ListStyle.Comma:
                Token(TokenRole.Comma, ",");
                break;
            case ListStyle.CommaLine:
                Token(TokenRole.Comma, ",");
                Break();
                break;
            case ListStyle.Line:
            case ListStyle.IndentedLines:
                Break();
                break;
            case ListStyle.Blank:
                BlankLine();
                break;
        }
    }

    // -----------------------------------------------------------------------------------
    // Embedded statements - R10
    // -----------------------------------------------------------------------------------

    /// <summary>
    /// Write a statement that sits in a statement-typed slot rather than in a block. A block
    /// writes itself; anything else takes its own line at one extra indent, which is what
    /// turns <c>if (x) return;</c> into two correctly indented lines.
    /// </summary>
    /// <param name="statement">The embedded statement.</param>
    /// <param name="chainable">
    /// True when the slot belongs to a clause that chains - the <c>else</c> of an
    /// <c>if</c>. An <c>if</c> in that slot stays on the <c>else</c> line so a ladder reads
    /// as <c>else if</c> rather than marching right.
    /// </param>
    public void Embedded(IStatement? statement, bool chainable = false)
    {
        if (statement == null)
        {
            return;
        }

        if (statement is IBlockLike)
        {
            statement.WriteOutput(_context);
            return;
        }

        if (chainable && statement is IElseChainable)
        {
            statement.WriteOutput(_context);
            return;
        }

        Break();
        _context.IncrementIndent();
        statement.WriteOutput(_context);
        Break();
        _context.DecrementIndent();
    }

    // -----------------------------------------------------------------------------------
    // THE SPACING POLICY
    //
    // Fourteen rules, in order. Everything above this point is plumbing; this is the part
    // that decides what the emitted C# looks like.
    // -----------------------------------------------------------------------------------

    private static bool NeedsSpace(TokenRole previous, TokenRole next)
    {
        // R2a: nothing takes a leading space before a closer, a separator, or member access.
        switch (next)
        {
            case TokenRole.CloseParen:
            case TokenRole.CloseParenCast:
            case TokenRole.CloseBracket:
            case TokenRole.CloseBracketAttr:
            case TokenRole.CloseAngle:
            case TokenRole.Comma:
            case TokenRole.SemiSeparator:
            case TokenRole.SemiTerminator:
            case TokenRole.SemiSection:
            case TokenRole.Dot:
            case TokenRole.ColonTight:
            case TokenRole.ColonLine:
            case TokenRole.QuestionTight:
            case TokenRole.PostfixOperator:
            case TokenRole.OpenAngle:
                return false;
        }

        // R2b: nothing takes a trailing space after an opener, member access, or prefix operator.
        switch (previous)
        {
            case TokenRole.None:
            case TokenRole.OpenParen:
            case TokenRole.OpenParenCast:
            case TokenRole.CloseParenCast:
            case TokenRole.OpenBracket:
            case TokenRole.OpenBracketAttr:
            case TokenRole.OpenAngle:
            case TokenRole.Dot:
            case TokenRole.PrefixOperator:
                return false;
        }

        // R3: `(` binds tight to a call target - an identifier, a closing bracket, or one of
        // the function-like keywords. After any other keyword it takes a space, which is the
        // whole difference between `typeof(int)` and `if (x)`.
        if (next == TokenRole.OpenParen || next == TokenRole.OpenParenCast)
        {
            switch (previous)
            {
                case TokenRole.Name:
                case TokenRole.Literal:
                case TokenRole.FnWord:
                case TokenRole.CloseParen:
                case TokenRole.CloseBracket:
                case TokenRole.CloseAngle:
                    return false;
                default:
                    return true;
            }
        }

        // R4: `[` binds tight to whatever it indexes or ranks - a name, a type keyword, a
        // closing bracket. Elsewhere (`= [1, 2]`, `case [x]:`) it takes a space.
        if (next == TokenRole.OpenBracket)
        {
            switch (previous)
            {
                case TokenRole.Name:
                case TokenRole.TypeWord:
                case TokenRole.Literal:
                case TokenRole.CloseParen:
                case TokenRole.CloseBracket:
                case TokenRole.CloseAngle:
                case TokenRole.QuestionTight:
                    return false;
                default:
                    return true;
            }
        }

        // R7a / R9b: a spaced colon, an inline brace, and an operator always separate.
        switch (next)
        {
            case TokenRole.Colon:
            case TokenRole.Operator:
            case TokenRole.PrefixOperator:
            case TokenRole.OpenBraceInline:
            case TokenRole.CloseBraceInline:
            case TokenRole.OpenBracketAttr:
                return true;
        }

        switch (previous)
        {
            case TokenRole.Colon:
            case TokenRole.Operator:
            case TokenRole.Comma:
            case TokenRole.SemiSeparator:
            case TokenRole.ColonTight:
            case TokenRole.OpenBraceInline:
            case TokenRole.CloseBraceInline:
            case TokenRole.PostfixOperator:
                return true;
        }

        // R1: two word-like tokens always separate - `public static void M`, `int x`,
        // `List<int> x`, `int[] x`, `int? x`.
        switch (next)
        {
            case TokenRole.Word:
            case TokenRole.FnWord:
            case TokenRole.Name:
            case TokenRole.TypeWord:
            case TokenRole.Literal:
                switch (previous)
                {
                    case TokenRole.Word:
                    case TokenRole.FnWord:
                    case TokenRole.Name:
                    case TokenRole.TypeWord:
                    case TokenRole.Literal:
                    case TokenRole.CloseParen:
                    case TokenRole.CloseBracket:
                    case TokenRole.CloseBracketAttr:
                    case TokenRole.CloseAngle:
                    case TokenRole.QuestionTight:
                    case TokenRole.Directive:
                        return true;
                }

                return false;
        }

        return false;
    }

    // -----------------------------------------------------------------------------------
    // R15: identifier escaping
    // -----------------------------------------------------------------------------------

    private static string Escape(string identifier)
    {
        if (identifier.Length == 0 || identifier[0] == '@' || !Keywords.Contains(identifier))
        {
            return identifier;
        }

        return "@" + identifier;
    }

    /// <summary>
    /// C#'s reserved keywords - the ones that are never legal as a bare identifier.
    /// Contextual keywords (<c>var</c>, <c>value</c>, <c>record</c>, <c>when</c>, ...) are
    /// deliberately absent: escaping those would be wrong, not merely noisy.
    /// </summary>
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
        "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this",
        "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
        "using", "virtual", "void", "volatile", "while",
    };
}
