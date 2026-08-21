#!/usr/bin/env python3
"""Report *code* references to Roslyn in C# source, ignoring prose.

A packaging leak is a compile-time reference: a `using` directive, a qualified type name,
an alias. It is not the words `Microsoft.CodeAnalysis` sitting inside an XML doc comment -
and several files in this library explain their relationship to Roslyn exactly that way,
because that relationship is the whole reason the bridge ships as a separate source folder:

    CSharpAuthor/BaseTypeDefinition.cs        <c>Microsoft.CodeAnalysis</c> in <remarks>
    CSharpAuthor/Profiles/LanguageVersion.cs  the same, twice
    CSharpAuthor/Profiles/EmitProfile.cs      the same
    CSharpAuthor/Profiles/ProfileEmitter.cs   the same

`grep -rl Microsoft.CodeAnalysis` reports all four and calls the package broken. That is a
false positive, and a check that cries wolf over its own documentation gets switched off.

So: blank out comments, string literals and char literals, then match what is left. The text
inside an interpolation hole is kept, because `$"{Describe(Microsoft.CodeAnalysis.X)}"` really
is code. A string's *contents* are not code - `Type.GetType("Microsoft.CodeAnalysis.X")` is
reflection, which imposes no build-time dependency and cannot make a package fail to compile.

usage:  roslyn-code-refs.py FILE...
output: one `path:line:source` row per hit
exit:   0 always - the caller decides what a hit means
"""

import re
import sys

PATTERN = re.compile(r"Microsoft\.CodeAnalysis|CSharpAuthor\.Roslyn")

# `$`, `@`, `$@`, `@$` and C# 11's `$$` all introduce a string. The lookbehind keeps
# `email@"..."`-shaped nonsense from matching mid-identifier.
_STRING_START = re.compile(r'(?P<prefix>[@$]{0,3})(?P<quotes>"+)')


def _blank(text):
    """The same text with every character replaced by a space, newlines kept.

    Offsets and line numbers therefore survive the blanking, so a hit's line number in the
    stripped text is its line number in the original.
    """
    return "".join("\n" if ch == "\n" else " " for ch in text)


def strip_noncode(src):
    """C# source with comments, string literals and char literals blanked out."""
    out = []
    i, n = 0, len(src)

    # One frame per open interpolated string that we are currently inside the holes of.
    # Each is [quote_count, verbatim, brace_depth]; brace_depth counts { } within the hole.
    holes = []

    while i < n:
        # Inside an interpolation hole: track braces so the closing one hands control back
        # to the string it came from.
        if holes and src[i] in "{}":
            frame = holes[-1]
            if src[i] == "{":
                frame[2] += 1
                out.append("{")
                i += 1
                continue
            frame[2] -= 1
            out.append("}")
            i += 1
            if frame[2] == 0:
                holes.pop()
                i = _scan_string(src, i, out, frame[0], frame[1], holes)
            continue

        if src.startswith("//", i):
            end = src.find("\n", i)
            end = n if end < 0 else end
            out.append(_blank(src[i:end]))
            i = end
            continue

        if src.startswith("/*", i):
            end = src.find("*/", i + 2)
            end = n if end < 0 else end + 2
            out.append(_blank(src[i:end]))
            i = end
            continue

        if src[i] == "'":
            j = i + 1
            while j < n and src[j] != "'" and src[j] != "\n":
                j += 2 if src[j] == "\\" else 1
            j = min(j + 1, n)
            out.append(_blank(src[i:j]))
            i = j
            continue

        if src[i] in '@$"':
            m = _STRING_START.match(src, i)
            if m and m.group("quotes"):
                prefix, quotes = m.group("prefix"), m.group("quotes")
                if prefix and i > 0 and (src[i - 1].isalnum() or src[i - 1] == "_"):
                    out.append(src[i])
                    i += 1
                    continue

                verbatim = "@" in prefix
                interpolated = "$" in prefix

                if verbatim:
                    # In a verbatim string "" is an escaped quote, so only the first quote
                    # opens it and the rest of the run belongs to the body.
                    i = m.start("quotes") + 1
                    out.append(_blank(prefix + '"'))
                    i = _scan_string(src, i, out, 1, True, holes if interpolated else None)
                    continue

                i = m.end("quotes")
                out.append(_blank(prefix + quotes))

                if len(quotes) >= 3:  # raw string literal, C# 11
                    close = '"' * len(quotes)
                    end = src.find(close, i)
                    end = n if end < 0 else end + len(close)
                    out.append(_blank(src[i:end]))
                    i = end
                    continue

                if len(quotes) == 2:  # "" - the empty string, already consumed
                    continue

                i = _scan_string(src, i, out, 1, False, holes if interpolated else None)
                continue

        out.append(src[i])
        i += 1

    return "".join(out)


def _scan_string(src, i, out, quotes, verbatim, holes):
    """Blank a string body from i. Stops at the closing quote, or at an interpolation hole.

    When it stops at a hole it pushes a frame onto `holes` and returns; the main loop then
    treats the hole's contents as code until the matching brace closes.
    """
    n = len(src)
    start = i
    while i < n:
        ch = src[i]

        if not verbatim and ch == "\\":
            i += 2
            continue

        if not verbatim and ch == "\n":
            break  # unterminated: stop here rather than swallowing the rest of the file

        if ch == '"':
            if verbatim and src.startswith('""', i):
                i += 2
                continue
            out.append(_blank(src[start:i + 1]))
            return i + 1

        if holes is not None and ch == "{":
            if src.startswith("{{", i):
                i += 2
                continue
            out.append(_blank(src[start:i]))
            out.append("{")
            holes.append([quotes, verbatim, 1])
            return i + 1

        i += 1

    out.append(_blank(src[start:i]))
    return i


def main(argv):
    if not argv:
        print(__doc__, file=sys.stderr)
        return 0

    for path in argv:
        try:
            with open(path, "r", encoding="utf-8-sig") as fh:
                src = fh.read()
        except OSError as err:
            print(f"{path}:0:cannot read: {err}")
            continue

        original = src.split("\n")
        for lineno, line in enumerate(strip_noncode(src).split("\n"), start=1):
            if PATTERN.search(line):
                print(f"{path}:{lineno}:{original[lineno - 1].strip()}")

    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
