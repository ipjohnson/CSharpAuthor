using System;
using System.Collections.Generic;

namespace CSharpAuthor.Expressions;

/// <summary>
/// Works out what precedence a piece of literal text deserves, so that a <see cref="Raw"/>
/// used as an operand is bracketed when leaving it bare would change the program.
/// </summary>
/// <remarks>
/// <para>
/// It answers one question: does the text consist of exactly one primary expression —
/// an atom followed by member accesses, calls, indexers and the other postfix forms —
/// possibly under a run of prefix unary operators? If yes it is
/// <see cref="ExPrecedence.Primary"/> (or <see cref="ExPrecedence.NullChain"/> when a
/// <c>?.</c> appeared, or <see cref="ExPrecedence.Unary"/> under a prefix operator). If
/// anything is left over — a binary operator, a comma, a cast, a lambda arrow, an
/// interpolation — the answer is <see cref="ExPrecedence.Lowest"/>, which brackets.
/// </para>
/// <para>
/// The asymmetry is deliberate. Guessing "primary" when the text is <c>a + b</c> silently
/// reassociates an expression; guessing "lowest" when the text is <c>Foo.Bar</c> costs a
/// redundant pair of brackets. Only one of those is a bug, so every uncertain case
/// resolves the same way. <see cref="Raw.At"/> is there for when you know better.
/// </para>
/// </remarks>
internal static class RawShape
{
    /// <summary>Words that may begin a primary expression.</summary>
    private static readonly HashSet<string> PrimaryKeywords = new HashSet<string>(StringComparer.Ordinal)
    {
        "true", "false", "null", "this", "base", "default",
    };

    /// <summary>Words that take a parenthesised argument and are primary as a whole.</summary>
    private static readonly HashSet<string> ParenthesisedKeywords = new HashSet<string>(StringComparer.Ordinal)
    {
        "typeof", "nameof", "sizeof", "checked", "unchecked",
    };

    /// <summary>
    /// Words that are certainly not the start of a lone primary expression. Anything
    /// beginning with one of these is left to the conservative answer.
    /// </summary>
    private static readonly HashSet<string> DisqualifyingKeywords = new HashSet<string>(StringComparer.Ordinal)
    {
        "is", "as", "switch", "with", "and", "or", "not", "throw", "ref", "out", "in",
        "var", "delegate", "from", "let", "where", "select", "group", "join", "orderby",
        "into", "on", "equals", "by", "ascending", "descending", "yield", "static",
        "async", "when", "using", "return", "if", "else", "while", "for", "foreach",
    };

    public static int Classify(string text, out ExFlags flags)
    {
        flags = ExFlags.None;

        if (string.IsNullOrEmpty(text))
        {
            return ExPrecedence.Primary;
        }

        var scanner = new Scanner(text);

        scanner.SkipWhitespace();

        if (scanner.AtEnd)
        {
            return ExPrecedence.Primary;
        }

        var lead = scanner.Current;

        if (lead == '-')
        {
            flags |= ExFlags.LeadsWithMinus;
        }
        else if (lead == '+')
        {
            flags |= ExFlags.LeadsWithPlus;
        }

        var precedence = scanner.ParseUnary();

        scanner.SkipWhitespace();

        if (precedence < 0 || !scanner.AtEnd)
        {
            return ExPrecedence.Lowest;
        }

        return precedence;
    }

    private sealed class Scanner
    {
        private const int Failed = -1;

        private readonly string _text;
        private int _index;
        private bool _nullConditional;

        public Scanner(string text)
        {
            _text = text;
            _index = 0;
        }

        public bool AtEnd => _index >= _text.Length;

        public char Current => _text[_index];

        private char At(int offset)
        {
            var position = _index + offset;

            return position < _text.Length ? _text[position] : '\0';
        }

        public void SkipWhitespace()
        {
            while (!AtEnd && char.IsWhiteSpace(Current))
            {
                _index++;
            }
        }

        /// <summary>A run of prefix unary operators wrapping a primary chain.</summary>
        public int ParseUnary()
        {
            SkipWhitespace();

            if (AtEnd)
            {
                return Failed;
            }

            var ch = Current;

            if (ch == '-' || ch == '+' || ch == '!' || ch == '~' || ch == '^' || ch == '&' || ch == '*')
            {
                // `!=` and `^=` are binary; neither can open an expression.
                if (At(1) == '=')
                {
                    return Failed;
                }

                _index++;

                if ((ch == '-' || ch == '+') && !AtEnd && Current == ch)
                {
                    _index++;
                }

                return ParseUnary() == Failed ? Failed : ExPrecedence.Unary;
            }

            if (IsWordStart(ch))
            {
                var save = _index;
                var word = ScanWord();

                if (string.Equals(word, "await", StringComparison.Ordinal))
                {
                    return ParseUnary() == Failed ? Failed : ExPrecedence.Unary;
                }

                _index = save;
            }

            return ParseChain();
        }

        private int ParseChain()
        {
            if (!ParseAtom())
            {
                return Failed;
            }

            ParsePostfix();

            return _nullConditional ? ExPrecedence.NullChain : ExPrecedence.Primary;
        }

        private bool ParseAtom()
        {
            SkipWhitespace();

            if (AtEnd)
            {
                return false;
            }

            var ch = Current;

            switch (ch)
            {
                case '"':
                    return ScanStringLiteral();

                case '\'':
                    return ScanCharLiteral();

                case '(':
                    return ScanBalanced('(', ')');

                case '[':
                    return ScanBalanced('[', ']');

                case '{':
                    return ScanBalanced('{', '}');

                case '@':
                    if (At(1) == '"')
                    {
                        _index++;
                        return ScanVerbatimStringLiteral();
                    }

                    if (IsWordStart(At(1)))
                    {
                        _index++;
                        ScanWord();
                        return true;
                    }

                    return false;

                // `$"…"` is an interpolated string; Ex.Interpolate builds those properly and
                // the escaping rules differ, so the classifier does not pretend to read them.
                case '$':
                    return false;
            }

            if (char.IsDigit(ch))
            {
                return ScanNumberLiteral();
            }

            if (!IsWordStart(ch))
            {
                return false;
            }

            var word = ScanWord();

            if (string.Equals(word, "new", StringComparison.Ordinal) ||
                string.Equals(word, "stackalloc", StringComparison.Ordinal))
            {
                SkipWhitespace();

                // `new()`, `new[] { … }`, `new { … }`, `new T(…)` — all continue into an atom,
                // and the postfix loop picks up the argument list or initializer.
                return !AtEnd && ParseAtom();
            }

            if (ParenthesisedKeywords.Contains(word))
            {
                SkipWhitespace();

                return !AtEnd && Current == '(' && ScanBalanced('(', ')');
            }

            if (PrimaryKeywords.Contains(word))
            {
                if (string.Equals(word, "default", StringComparison.Ordinal))
                {
                    SkipWhitespace();

                    if (!AtEnd && Current == '(')
                    {
                        return ScanBalanced('(', ')');
                    }
                }

                return true;
            }

            return !DisqualifyingKeywords.Contains(word);
        }

        private void ParsePostfix()
        {
            while (true)
            {
                var save = _index;

                SkipWhitespace();

                if (AtEnd)
                {
                    _index = save;
                    return;
                }

                var ch = Current;

                if (ch == '.' && At(1) != '.')
                {
                    if (!TryConsumeMemberName(1))
                    {
                        _index = save;
                        return;
                    }

                    continue;
                }

                if (ch == '?' && At(1) == '.')
                {
                    if (!TryConsumeMemberName(2))
                    {
                        _index = save;
                        return;
                    }

                    _nullConditional = true;
                    continue;
                }

                if (ch == ':' && At(1) == ':')
                {
                    if (!TryConsumeMemberName(2))
                    {
                        _index = save;
                        return;
                    }

                    continue;
                }

                if (ch == '?' && At(1) == '[')
                {
                    _index++;

                    if (!ScanBalanced('[', ']'))
                    {
                        return;
                    }

                    _nullConditional = true;
                    continue;
                }

                if (ch == '(')
                {
                    if (!ScanBalanced('(', ')'))
                    {
                        return;
                    }

                    continue;
                }

                if (ch == '[')
                {
                    if (!ScanBalanced('[', ']'))
                    {
                        return;
                    }

                    continue;
                }

                if (ch == '{')
                {
                    if (!ScanBalanced('{', '}'))
                    {
                        return;
                    }

                    continue;
                }

                // The null-forgiving operator, but not `!=`.
                if (ch == '!' && At(1) != '=')
                {
                    _index++;
                    continue;
                }

                if ((ch == '+' && At(1) == '+') || (ch == '-' && At(1) == '-'))
                {
                    _index += 2;
                    continue;
                }

                if (ch == '<' && TryScanTypeArguments())
                {
                    continue;
                }

                _index = save;
                return;
            }
        }

        private bool TryConsumeMemberName(int operatorLength)
        {
            var save = _index;

            _index += operatorLength;

            SkipWhitespace();

            if (AtEnd)
            {
                _index = save;
                return false;
            }

            if (Current == '@' && IsWordStart(At(1)))
            {
                _index++;
                ScanWord();
                return true;
            }

            if (!IsWordStart(Current))
            {
                _index = save;
                return false;
            }

            ScanWord();
            return true;
        }

        /// <summary>
        /// A type argument list, <c>&lt;int, List&lt;string&gt;&gt;</c>. Only characters that
        /// can appear in a type are accepted, so <c>a &lt; b</c> is not mistaken for one.
        /// </summary>
        private bool TryScanTypeArguments()
        {
            var save = _index;
            var depth = 0;

            while (!AtEnd)
            {
                var ch = Current;

                if (ch == '<')
                {
                    depth++;
                    _index++;
                    continue;
                }

                if (ch == '>')
                {
                    depth--;
                    _index++;

                    if (depth == 0)
                    {
                        return true;
                    }

                    continue;
                }

                var typeCharacter =
                    char.IsLetterOrDigit(ch) || ch == '_' || ch == '.' || ch == ',' ||
                    ch == '?' || ch == '[' || ch == ']' || ch == ':' || ch == '@' ||
                    ch == '*' || char.IsWhiteSpace(ch);

                if (!typeCharacter)
                {
                    break;
                }

                _index++;
            }

            _index = save;
            return false;
        }

        private bool ScanBalanced(char open, char close)
        {
            if (AtEnd || Current != open)
            {
                return false;
            }

            var depth = 0;

            while (!AtEnd)
            {
                var ch = Current;

                if (ch == '"')
                {
                    if (!ScanStringLiteral())
                    {
                        return false;
                    }

                    continue;
                }

                if (ch == '\'')
                {
                    if (!ScanCharLiteral())
                    {
                        return false;
                    }

                    continue;
                }

                if (ch == '@' && At(1) == '"')
                {
                    _index++;

                    if (!ScanVerbatimStringLiteral())
                    {
                        return false;
                    }

                    continue;
                }

                if (ch == open)
                {
                    depth++;
                }
                else if (ch == close)
                {
                    depth--;

                    if (depth == 0)
                    {
                        _index++;
                        return true;
                    }
                }

                _index++;
            }

            return false;
        }

        private bool ScanStringLiteral()
        {
            _index++;

            while (!AtEnd)
            {
                var ch = Current;

                if (ch == '\\')
                {
                    _index += 2;
                    continue;
                }

                _index++;

                if (ch == '"')
                {
                    return true;
                }
            }

            return false;
        }

        private bool ScanVerbatimStringLiteral()
        {
            _index++;

            while (!AtEnd)
            {
                if (Current == '"')
                {
                    if (At(1) == '"')
                    {
                        _index += 2;
                        continue;
                    }

                    _index++;
                    return true;
                }

                _index++;
            }

            return false;
        }

        private bool ScanCharLiteral()
        {
            _index++;

            while (!AtEnd)
            {
                var ch = Current;

                if (ch == '\\')
                {
                    _index += 2;
                    continue;
                }

                _index++;

                if (ch == '\'')
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A numeric literal. The fractional point is only taken when a digit follows, so
        /// <c>1..2</c> stays a range and <c>1.ToString()</c> stays a call.
        /// </summary>
        private bool ScanNumberLiteral()
        {
            if (Current == '0' && (At(1) == 'x' || At(1) == 'X' || At(1) == 'b' || At(1) == 'B'))
            {
                _index += 2;

                while (!AtEnd && (IsHexDigit(Current) || Current == '_'))
                {
                    _index++;
                }
            }
            else
            {
                while (!AtEnd && (char.IsDigit(Current) || Current == '_'))
                {
                    _index++;
                }

                if (!AtEnd && Current == '.' && char.IsDigit(At(1)))
                {
                    _index++;

                    while (!AtEnd && (char.IsDigit(Current) || Current == '_'))
                    {
                        _index++;
                    }
                }

                if (!AtEnd && (Current == 'e' || Current == 'E'))
                {
                    var save = _index;

                    _index++;

                    if (!AtEnd && (Current == '+' || Current == '-'))
                    {
                        _index++;
                    }

                    if (!AtEnd && char.IsDigit(Current))
                    {
                        while (!AtEnd && char.IsDigit(Current))
                        {
                            _index++;
                        }
                    }
                    else
                    {
                        _index = save;
                    }
                }
            }

            while (!AtEnd && IsNumericSuffix(Current))
            {
                _index++;
            }

            return true;
        }

        private string ScanWord()
        {
            var start = _index;

            while (!AtEnd && (char.IsLetterOrDigit(Current) || Current == '_'))
            {
                _index++;
            }

            return _text.Substring(start, _index - start);
        }

        private static bool IsWordStart(char ch) => char.IsLetter(ch) || ch == '_';

        private static bool IsHexDigit(char ch) =>
            char.IsDigit(ch) || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F');

        private static bool IsNumericSuffix(char ch) =>
            ch == 'f' || ch == 'F' || ch == 'd' || ch == 'D' ||
            ch == 'm' || ch == 'M' || ch == 'u' || ch == 'U' ||
            ch == 'l' || ch == 'L';
    }
}
