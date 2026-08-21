#!/usr/bin/env python3
"""
Generate CSharpAuthor's grammar node layer from Roslyn's Syntax.xml.

    python3 tools/grammar/gen_all.py

Writes CSharpAuthor/Syntax/Nodes.g.cs. Never edit that file: fix this script and
re-run. A new C# language version is a regeneration, not a rewrite.

Two inputs:
  Syntax.xml   Roslyn's grammar. Field order in a <Node> IS emit order - that is
               the whole trick. Copied verbatim from the Roslyn repo.
  tokens.json  SyntaxKind -> text, harvested from SyntaxFacts.GetText. The XML
               names token kinds but never gives their spelling.

What the grammar does NOT encode is whitespace, and it never will. This script
assigns every emitted token a TokenRole; SyntaxWriter turns roles into spacing.
Every role assignment below is a *category* rule - keyed on a field's type, its
position in the node, or the node's base chain. None is keyed on a node name, so
a new C# version adds nodes without invalidating any of them.

HARD RULE - the emitted source must compile as **C# 10 or lower**, on
netstandard2.0, under EnforceExtendedAnalyzerRules. CSharpAuthor.csproj pins
LangVersion 10, and both consumers source-compile this file into generator
projects that pin 10 and 11. So: no collection expressions, no primary
constructors, no `required`, no raw string literals, no `field` keyword, and
nothing from System.IO, System.Environment or a culture-sensitive overload
(RS1035 is an error in the consumer build and is invisible here). Raising
LangVersion to make generation compile passes this repo's tests and breaks the
consumers silently - never do it.

File-scoped namespaces and target-typed `new()` are C# 10 and C# 9, so both are
in bounds and both are used.
"""

import json
import os
import re
import sys
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(ROOT, 'CSharpAuthor', 'Syntax', 'Nodes.g.cs')

TOKENS = json.load(open(os.path.join(HERE, 'tokens.json')))
TREE = ET.parse(os.path.join(HERE, 'Syntax.xml'))
ROOT_EL = TREE.getroot()

# ---------------------------------------------------------------------------
# Grammar model
# ---------------------------------------------------------------------------

concrete = {}   # name -> element
abstract = {}   # name -> element

for el in ROOT_EL:
    if el.tag == 'Node':
        concrete[el.get('Name')] = el
    elif el.tag == 'AbstractNode':
        abstract[el.get('Name')] = el

ROOTS = {'CSharpSyntaxNode', 'SyntaxNode', 'StructuredTriviaSyntax'}


def base_of(name):
    el = concrete.get(name)
    if el is None:
        el = abstract.get(name)
    return el.get('Base') if el is not None else None


def chain(name):
    """Every base of `name`, nearest first."""
    result, cur = [], base_of(name)
    while cur:
        result.append(cur)
        cur = base_of(cur)
    return result


def iface_name(syntax_name):
    return 'I' + re.sub(r'Syntax$', '', syntax_name)


def class_name(syntax_name):
    return re.sub(r'Syntax$', '', syntax_name)


def fields(el):
    """
    Every field of a node, flattened into document order - which is emit order.

    The grammar nests alternatives in <Choice> and runs of them in <Sequence>. A
    field inside a Choice is one alternative among several (a method has a block
    body *or* an expression body *or* a bare semicolon), so it is optional whatever
    the XML's Optional attribute says. Reading only direct <Field> children loses
    them, and losing them costs you method bodies, `for` initialisers, lambda
    bodies and property accessor lists - which is exactly what the prototype's
    extractor did.

    Returns (element, forced_optional) pairs.
    """
    result = []

    def walk(node, forced_optional):
        for child in node:
            if child.tag == 'Field':
                result.append((child, forced_optional))
            elif child.tag in ('Choice', 'Sequence'):
                walk(child, True)

    walk(el, False)
    return result


def kinds_of(field):
    """
    The kinds a token field may hold.

    A handful of fields declare none - `ColonToken` on the abstract expression-colon
    node, `CommaToken` on a Cref parameter. Roslyn's convention is that the field
    name *is* the kind there, so resolve it that way rather than asking the caller
    to supply a colon.
    """
    kinds = [k.get('Name') for k in field.findall('Kind')]

    # RecordDeclarationSyntax.Keyword declares <ContextualKind>, not <Kind> - it is the
    # only field in the grammar that does. Without this it resolves to no kind at all and
    # falls through to a raw value token, which emits `publicrecordFoo(...)` with no spaces.
    if not kinds:
        kinds = [k.get('Name') for k in field.findall('ContextualKind')]

    if not kinds and field.get('Name') in TOKENS:
        return [field.get('Name')]

    return kinds


# The abstract nodes we surface as interfaces. Everything reachable as a base of
# something concrete, plus the roots, mapped to ISyntax.
def iface_for(syntax_name):
    """
    How a value of this grammar type is declared. An abstract grammar type becomes
    an interface; a concrete one is named directly, because only one class can ever
    fill that slot.
    """
    if syntax_name is None or syntax_name in ROOTS:
        return 'ISyntax'
    if syntax_name in abstract:
        return iface_name(syntax_name)
    if syntax_name in concrete:
        return class_name(syntax_name)
    return 'ISyntax'


def category(name):
    """
    The node's broad category, from its base chain. Drives brace style, colon
    style, attribute-list placement and the type-node token rule.
    """
    c = chain(name)
    # Checked first: an interpolated string is an expression, but nothing inside one
    # takes a space - `$"count is {count:N2}"` - so the family wins over the chain.
    if name.startswith('Interpolat') or 'InterpolatedStringContentSyntax' in c:
        return 'interpolated'
    if 'StatementSyntax' in c:
        return 'statement'
    if 'MemberDeclarationSyntax' in c:
        return 'member'
    if 'TypeSyntax' in c:
        return 'type'
    if 'PatternSyntax' in c:
        return 'pattern'
    if 'ExpressionSyntax' in c:
        return 'expression'
    if 'SwitchLabelSyntax' in c:
        return 'switchlabel'
    if 'DirectiveTriviaSyntax' in c:
        return 'directive'
    if 'XmlNodeSyntax' in c or 'XmlAttributeSyntax' in c or name.startswith('Xml'):
        return 'xml'
    if 'CrefSyntax' in c or 'MemberCrefSyntax' in c or name.startswith('Cref'):
        return 'cref'
    if name in ('CompilationUnitSyntax',):
        return 'container'
    if name.endswith('ClauseSyntax'):
        return 'clause'
    return 'other'


# Categories whose braces are Allman blocks. Everything else keeps its braces
# inline: initializers, `with`, property patterns, anonymous objects.
BLOCK_BRACE_CATEGORIES = {'statement', 'member', 'container'}


def block_braced(name, el):
    """
    R9: does this node's brace pair open a block, or stay on the line?

    Two structural signals, no node names. A statement, member or compilation
    unit always blocks. So does any other node whose braces enclose an
    *unseparated* list of nodes - which is what separates an accessor list or a
    switch body (one entry per line) from an object initialiser or a property
    pattern (comma-separated, and idiomatically inline).
    """
    if category(name) in BLOCK_BRACE_CATEGORIES:
        return True
    for f, _ in fields(el):
        ftype = f.get('Type')
        if ftype.startswith('SyntaxList<') and ftype != 'SyntaxList<SyntaxToken>':
            return True
    return False


# Delimiters that the grammar marks optional but a caller almost always wants:
# a type declaration's braces are Optional only because `class C;` exists.
PAIRED_DELIMITERS = {
    'OpenBraceToken', 'CloseBraceToken', 'OpenParenToken', 'CloseParenToken',
    'OpenBracketToken', 'CloseBracketToken', 'LessThanToken', 'GreaterThanToken',
}

# ---------------------------------------------------------------------------
# Token roles - all structural
# ---------------------------------------------------------------------------

# Tokens with no spelling at all. They mark a position in the grammar, not text.
ZERO_WIDTH = {
    'EndOfFileToken': 'break',
    'EndOfDirectiveToken': 'break',
    'EndOfDocumentationCommentToken': 'break',
    'OmittedTypeArgumentToken': 'none',
    'OmittedArraySizeExpressionToken': 'none',
}

# Keywords that bind their parenthesis tight. `typeof(int)`, never `typeof (int)`.
FN_WORDS = {
    'typeof', 'nameof', 'sizeof', 'default', 'checked', 'unchecked', 'stackalloc',
    '__makeref', '__reftype', '__refvalue', '__arglist',
    # `new` belongs here for `new()` and `new[] { 1 }`; it still separates from a type
    # name, because that is the word rule rather than the parenthesis rule.
    'new',
}

PREFIX_ONLY_KINDS = {
    'ExclamationToken', 'TildeToken', 'PlusPlusToken', 'MinusMinusToken',
    'AmpersandToken', 'CaretToken', 'AsteriskToken',
}

PUNCT_ROLE = {
    'OpenParenToken': 'OpenParen',
    'CloseParenToken': 'CloseParen',
    'OpenBracketToken': 'OpenBracket',
    'CloseBracketToken': 'CloseBracket',
    'LessThanToken': 'OpenAngle',
    'GreaterThanToken': 'CloseAngle',
    'CommaToken': 'Comma',
    'DotToken': 'Dot',
    'ColonColonToken': 'Dot',
    'MinusGreaterThanToken': 'Dot',
    'QuestionToken': 'QuestionTight',
    'HashToken': 'Directive',
    'DotDotToken': 'RangeDots',
}

# Keywords that stand where an identifier would, so they bind to `(` and `[` the same
# way: the receiver of `this[0]`, the name of an indexer, the target of `base(...)`.
NAME_LIKE_KEYWORD_KINDS = {'ThisKeyword', 'BaseKeyword'}


def token_role(node_name, field, index, flat, node_category, blocks):
    """
    The role for a token field. Everything here reads the grammar, never a node
    name: the token's kind, whether the field is last, and the node's category.
    """
    name = field.get('Name')
    ks = kinds_of(field)
    count = len(flat)
    text = TOKENS.get(ks[0]) if len(ks) == 1 else None

    # Everything inside an interpolated string abuts its neighbour, braces included.
    if node_category == 'interpolated':
        return 'Tight'

    # `this` and `base` name a member or a receiver, so they bind to `(` and `[`.
    if len(ks) == 1 and ks[0] in NAME_LIKE_KEYWORD_KINDS:
        return 'BareName'

    # The `+` of `operator +(...)` names the member being declared.
    if name == 'OperatorToken' and node_category == 'member':
        return 'BareName'

    # R8: a semicolon that ends a node terminates a line.
    #
    # One in the middle is either a section header's - `namespace Acme;` before the
    # file's types - or a genuine separator, and the node's category tells them
    # apart: only a member or a compilation unit has a body to introduce. Anywhere
    # else, a mid-node semicolon is one of the two in `for (;;)`.
    if 'SemicolonToken' in ks:
        if index == count - 1:
            return 'SemiTerminator'
        if node_category in ('member', 'container'):
            return 'SemiSection'
        return 'SemiSeparator'

    if 'OpenBraceToken' in ks:
        return 'OpenBrace' if blocks else 'OpenBraceInline'
    if 'CloseBraceToken' in ks:
        return 'CloseBrace' if blocks else 'CloseBraceInline'

    # R6: `?` is a nullable marker in a type (`int?`), the head of a ternary
    # (`a ? b : c`), or a null-conditional access (`a?.b`). The grammar tells the
    # three apart: only the ternary carries a matching colon in the same node.
    if ks == ['QuestionToken']:
        if node_category == 'type':
            return 'QuestionTight'
        if any('ColonToken' in kinds_of(g) for g, _ in flat):
            return 'Operator'
        return 'QuestionTight'

    # R7: colons.
    if ks == ['ColonToken']:
        if node_category in ('switchlabel',) or node_name == 'LabeledStatementSyntax':
            return 'ColonLine'
        if node_name.endswith('ColonSyntax') or node_name in (
                'AttributeTargetSpecifierSyntax', 'InterpolationFormatClauseSyntax'):
            return 'ColonTight'
        return 'Colon'

    # `(`/`)` of a cast: an expression node whose closing paren is preceded by a
    # type slot and followed by more fields. That shape is a cast and only a cast,
    # so `(int)x` binds and `(a) + b` does not.
    if ks == ['CloseParenToken'] and node_category == 'expression' and index < count - 1:
        if index > 0 and flat[index - 1][0].get('Type') in TYPE_SLOTS:
            return 'CloseParenCast'

    # `[`/`]` of an attribute list.
    if node_name == 'AttributeListSyntax':
        if ks == ['OpenBracketToken']:
            return 'OpenBracketAttr'
        if ks == ['CloseBracketToken']:
            return 'CloseBracketAttr'

    # One kind, or several that all mean the same punctuation. `.` and `->` are both
    # member access, so an OperatorToken offering only those two is a `.`, not a binary
    # operator - which is the difference between `a.B` and `a . B`.
    punct = {PUNCT_ROLE[k] for k in ks if k in PUNCT_ROLE}
    if len(punct) == 1 and all(k in PUNCT_ROLE for k in ks):
        return next(iter(punct))

    if text is not None:
        if text[0].isalpha() or text[0] == '_':
            if text in FN_WORDS:
                return 'FnWord'
            # R5-adjacent: a keyword inside a type node names a type, so `int[]`
            # and `int?` bind tight the way an identifier does.
            return 'BareName' if node_category == 'type' else 'Word'
        if ks[0] in PREFIX_ONLY_KINDS and name in ('OperatorToken', 'RefKindKeyword'):
            return 'PrefixOperator'
        return 'Operator'

    # No single spelling: the caller supplies the text. Identifiers are names,
    # literals are literals, an operator slot is an operator.
    if 'IdentifierToken' in ks and len(ks) <= 2:
        return 'Name'
    if any(k.endswith('LiteralToken') for k in ks):
        return 'Literal'
    if name == 'OperatorToken':
        # Where the operator sits relative to its operand is the whole difference
        # between `!x`, `x++` and `a + b`, and the grammar states it: first field,
        # last field, or between two operands.
        # A relational pattern also leads with its operator - `is >= 0` - but there it
        # separates from the operand rather than binding to it.
        if index == 0:
            return 'PrefixOperator' if node_category == 'expression' else 'Operator'
        if index == count - 1:
            return 'PostfixOperator'
        return 'Operator'
    if not ks:
        return 'Raw'
    if all(TOKENS.get(k, '?')[:1].isalpha() for k in ks if k in TOKENS) and any(k in TOKENS for k in ks):
        # A keyword slot inside a type node names a type - `int`, `string`, `void` -
        # so it binds to `[` and `?` the way an identifier does.
        return 'BareName' if node_category == 'type' else 'Word'
    return 'Operator'


# ---------------------------------------------------------------------------
# List styles - all derived from the element type and the container's category
# ---------------------------------------------------------------------------

def list_style(element_type, separated, node_category, container_braced):
    if separated:
        # R12
        if element_type == 'EnumMemberDeclarationSyntax':
            return 'CommaLine'
        return 'Comma'

    # R11
    if element_type == 'AttributeListSyntax':
        return 'LineEach' if node_category in ('member', 'statement', 'container') else 'None'
    if element_type in ('UsingDirectiveSyntax', 'ExternAliasDirectiveSyntax'):
        return 'UsingBlock'
    if element_type == 'TypeParameterConstraintClauseSyntax':
        return 'IndentedLines'
    if element_type == 'SyntaxToken':
        return 'None'
    c = [element_type] + chain(element_type)
    if 'MemberDeclarationSyntax' in c:
        return 'Blank'
    if 'StatementSyntax' in c:
        # A block's braces already opened a scope, so its statements only need a line
        # break. A switch section has no braces of its own, so its statements have to
        # carry the indent themselves.
        return 'Line' if container_braced else 'IndentedLines'
    if element_type in ('SwitchSectionSyntax', 'CatchClauseSyntax', 'AccessorDeclarationSyntax',
                        'QueryClauseSyntax', 'SwitchLabelSyntax'):
        return 'Line'
    return 'None'


# ---------------------------------------------------------------------------
# Emit
# ---------------------------------------------------------------------------

TYPE_SLOTS = {'TypeSyntax', 'NameSyntax', 'SimpleNameSyntax', 'IdentifierNameSyntax', 'ArrayTypeSyntax'}

CS_KEYWORDS = set('''abstract as base bool break byte case catch char checked class const continue decimal
default delegate do double else enum event explicit extern false finally fixed float for foreach goto if
implicit in int interface internal is lock long namespace new null object operator out override params
private protected public readonly ref return sbyte sealed short sizeof stackalloc static string struct
switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while'''.split())


def camel(name):
    lo = name[0].lower() + name[1:]
    return '@' + lo if lo in CS_KEYWORDS else lo


out = []
w = out.append

w('// <auto-generated>')
w('//   Generated from Roslyn\'s Syntax.xml by tools/grammar/gen_all.py.')
w('//   Do not edit. Fix the generator and re-run:  python3 tools/grammar/gen_all.py')
w('//')
w('//   Field order in the grammar IS emit order; that is the whole trick. What the')
w('//   grammar cannot encode is whitespace - that lives in SyntaxWriter, and every')
w('//   TokenRole below was assigned by a category rule, never by node name.')
w('// </auto-generated>')
w('#nullable enable')
w('')
w('using System.Collections.Generic;')
w('')
w('namespace CSharpAuthor.Syntax;')
w('')

# --- interfaces -------------------------------------------------------------

w('/// <summary>Root of the generated grammar. Every node is an <see cref="IOutputComponent"/>.</summary>')
w('#if CSHARPAUTHOR_PUBLIC_SYNTAX')
w('public')
w('#endif')
w('interface ISyntax : IOutputComponent { }')
w('')
w('/// <summary>A statement that brings its own braces, so it is never re-indented as an embedded statement.</summary>')
w('#if CSHARPAUTHOR_PUBLIC_SYNTAX')
w('public')
w('#endif')
w('interface IBlockLike : ISyntax { }')
w('')
w('/// <summary>A statement that may follow <c>else</c> on the same line.</summary>')
w('#if CSHARPAUTHOR_PUBLIC_SYNTAX')
w('public')
w('#endif')
w('interface IElseChainable : ISyntax { }')
w('')

# Only abstract nodes that are actually referenced need an interface, but emitting
# all of them costs one line each and keeps the hierarchy faithful.
emitted_ifaces = set()
for name in sorted(abstract):
    b = base_of(name)
    parent = iface_for(b) if b not in ROOTS and b is not None else 'ISyntax'
    if parent == iface_name(name):
        parent = 'ISyntax'
    w('#if CSHARPAUTHOR_PUBLIC_SYNTAX')
    w('public')
    w('#endif')
    w(f'interface {iface_name(name)} : {parent} {{ }}')
    emitted_ifaces.add(iface_name(name))
w('')

# --- nodes ------------------------------------------------------------------

stats = {
    'nodes': 0, 'value_tokens': 0, 'nodes_with_value_token': 0,
    'fixed_tokens': 0, 'lists': 0, 'separated_lists': 0, 'type_slots': 0,
    'skipped_bool': 0, 'optional_tokens': 0,
}
experimental = []

for name in sorted(concrete):
    el = concrete[name]
    cls = class_name(name)
    cat = category(name)
    fs = fields(el)
    blocks = block_braced(name, el)
    braced = any('OpenBraceToken' in kinds_of(f) for f, _ in fs)
    base = base_of(name)
    parent = iface_for(base)

    markers = []
    if cat == 'statement' and any('OpenBraceToken' in kinds_of(f) for f, _ in fs):
        markers.append('IBlockLike')
    if any(f.get('Type') == 'ElseClauseSyntax' for f, _ in fs):
        markers.append('IElseChainable')

    props, ctor_params, ctor_assign, body = [], [], [], []
    has_value_token = False

    for i, (f, forced_optional) in enumerate(fs):
        fname = f.get('Name')
        ftype = f.get('Type')
        optional = forced_optional or f.get('Optional') == 'true'
        lo = camel(fname)
        ks = kinds_of(f)

        if ftype == 'bool':
            # Directive bookkeeping (IsActive, BranchTaken, ConditionValue). No text.
            stats['skipped_bool'] += 1
            continue

        if ftype == 'SyntaxToken':
            zero = next((k for k in ks if k in ZERO_WIDTH), None)
            if zero and len(ks) == 1:
                if ZERO_WIDTH[zero] == 'break':
                    body.append('        writer.Break();')
                continue

            role = token_role(name, f, i, fs, cat, blocks)
            text = TOKENS.get(ks[0]) if len(ks) == 1 else None

            if text is not None:
                stats['fixed_tokens'] += 1
                emit = f'writer.Token(TokenRole.{role}, {json.dumps(text)});'
                if optional:
                    # The grammar says this token may be absent, and the spelling is
                    # fixed, so presence is the only thing left to carry. A delimiter
                    # defaults on, a keyword defaults off.
                    default = ' = true;' if ks[0] in PAIRED_DELIMITERS else ''
                    stats['optional_tokens'] += 1
                    props.append(f'    public bool {fname} {{ get; set; }}{default}')
                    body.append(f'        if ({fname}) {{ {emit} }}')
                else:
                    body.append(f'        {emit}')
            else:
                has_value_token = True
                stats['value_tokens'] += 1
                props.append(f'    public string {fname} {{ get; set; }} = "";')
                if not optional:
                    ctor_params.append(f'string {lo}')
                    ctor_assign.append(f'        {fname} = {lo};')
                body.append(f'        writer.Token(TokenRole.{role}, {fname});')
            continue

        if ftype == 'SyntaxList<SyntaxToken>':
            role = 'Word' if fname == 'Modifiers' else 'Raw'
            props.append(f'    public List<string> {fname} {{ get; }} = new();')
            body.append(f'        writer.Tokens({fname}, TokenRole.{role});')
            continue

        m = re.match(r'^(Separated)?SyntaxList<(.+)>$', ftype)
        if m:
            separated = m.group(1) is not None
            element = m.group(2)
            style = list_style(element, separated, cat, braced)
            elem_iface = iface_for(element)
            stats['lists'] += 1
            if separated:
                stats['separated_lists'] += 1
            # R12a: a NodeList is a List that can also say the source ended with a separator.
            # `{ 1, 2, }` is legal C# and Roslyn keeps the extra comma as a token, so a list that
            # cannot carry the fact is a list that silently drops it. Every list gets the type -
            # one type for one concept - and only a SeparatedSyntaxList is ever asked to set it.
            note = f'{style}, separated' if separated else str(style)
            props.append(f'    public NodeList<{elem_iface}> {fname} {{ get; }} = new();  // {note}')
            body.append(f'        writer.List({fname}, ListStyle.{style});')
            continue

        if ftype in TYPE_SLOTS:
            stats['type_slots'] += 1
            props.append(f'    public TypeRef {fname} {{ get; set; }}')
            if not optional:
                ctor_params.append(f'TypeRef {lo}')
                ctor_assign.append(f'        {fname} = {lo};')
            body.append(f'        writer.Type({fname});')
            continue

        # A plain child node.
        elem_iface = iface_for(ftype)
        props.append(f'    public {elem_iface}? {fname} {{ get; set; }}')
        if not optional:
            ctor_params.append(f'{elem_iface} {lo}')
            ctor_assign.append(f'        {fname} = {lo};')

        # R10: a statement in a statement-typed slot is an *embedded* statement - the
        # body of an `if` or a `while`, which takes its own indented line when it is
        # not a block. A statement slot on a member declaration is not that: a
        # top-level statement is the member, and indenting it would be wrong.
        if ftype == 'StatementSyntax' and cat in ('statement', 'clause'):
            chainable = 'true' if name.endswith('ClauseSyntax') else 'false'
            body.append(f'        writer.Embedded({fname}, {chainable});')
        else:
            body.append(f'        writer.Node({fname});')

    if has_value_token:
        stats['nodes_with_value_token'] += 1
    if el.get('ExperimentalUrl'):
        experimental.append(name)

    implements = ', '.join(['SyntaxNode', parent] + markers)

    w(f'/// <summary>{cls} - <c>{name}</c> in the grammar.</summary>')
    w('#if CSHARPAUTHOR_PUBLIC_SYNTAX')
    w('public')
    w('#endif')
    w(f'sealed class {cls} : {implements}')
    w('{')
    for p in props:
        w(p)
    if ctor_params:
        w(f'    public {cls}({", ".join(ctor_params)})')
        w('    {')
        for a in ctor_assign:
            w(a)
        w('    }')
        w('')
        w(f'    public {cls}() {{ }}')
    else:
        w(f'    public {cls}() {{ }}')
    w('')
    w('    public override void WriteOutput(IOutputContext outputContext)')
    w('    {')
    if body:
        w('        var writer = SyntaxWriter.For(outputContext);')
        w('')
        for b in body:
            w(b)
    w('    }')
    w('}')
    w('')
    stats['nodes'] += 1

if '--report' not in sys.argv:
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    open(OUT, 'w').write('\n'.join(out) + '\n')

# ---------------------------------------------------------------------------
# Coverage report - V2-HANDOFF.md 9(a). `--report` prints it and writes nothing.
# ---------------------------------------------------------------------------

if '--report' in sys.argv:
    import collections

    with open(os.path.join(HERE, 'Syntax.xml')) as handle:
        raw = handle.read()
    # Only the ones that exist *nowhere else*. A node can appear both live and inside
    # a commented-out alternative spelling; that one is emitted and is not a gap.
    commented = sorted({
        name
        for block in re.findall(r'<!--(.*?)-->', raw, re.S)
        for name in re.findall(r'<Node Name="(\w+)"', block)
    } - set(concrete))

    by_category = collections.Counter(category(n) for n in concrete)
    experimental_nodes = sorted(n for n in concrete if concrete[n].get('ExperimentalUrl'))
    print()
    print('C# NODE COVERAGE  (V2-HANDOFF.md 9(a))')
    print('=' * 72)
    print(f'concrete <Node> declarations in Syntax.xml : {len(concrete)}')
    print(f'node classes emitted                       : {stats["nodes"]}')
    print(f'coverage                                   : '
          f'{100.0 * stats["nodes"] / len(concrete):.1f}%')
    print(f'declared only inside an XML comment         : {len(commented)}'
          + (f'  ({", ".join(commented)})' if commented else ''))
    print()
    print('by category:')
    for cat_name, count in sorted(by_category.items(), key=lambda kv: -kv[1]):
        print(f'  {cat_name:14} {count:4}')
    print()
    print(f'nodes taking a caller-supplied token value : {stats["nodes_with_value_token"]}'
          f'  ({stats["value_tokens"]} such tokens)')
    print('  (an identifier, a literal, or an operator - the grammar names the slot')
    print('   but gives no spelling, so the caller supplies one)')
    print()
    print(f'experimental, emitted but not parseable by a shipping compiler: '
          f'{len(experimental_nodes)}')
    for n in experimental_nodes:
        print(f'  {n}  {concrete[n].get("ExperimentalUrl")}')
    print()
    print('NOT expressible as a node, by construction:')
    print('  //  and  /* */  comments - trivia in Roslyn, no <Node> exists for them.')
    print('     Reachable through Raw, and through DocumentationCommentTrivia for /// docs.')
    print('  #if / #endif spans - the directive nodes emit, but a region wraps an')
    print('     arbitrary span of other nodes rather than containing them, so the tree')
    print('     cannot nest one. Reachable through Raw.')
    print('=' * 72)
    sys.exit(0)

print(f'nodes in grammar   : {len(concrete)}', file=sys.stderr)
print(f'nodes emitted      : {stats["nodes"]}', file=sys.stderr)
print(f'interfaces emitted : {len(emitted_ifaces) + 3}', file=sys.stderr)
print(f'fixed tokens       : {stats["fixed_tokens"]}', file=sys.stderr)
print(f'value tokens       : {stats["value_tokens"]} across {stats["nodes_with_value_token"]} nodes', file=sys.stderr)
print(f'optional tokens    : {stats["optional_tokens"]}', file=sys.stderr)
print(f'grammar lists      : {stats["lists"]} ({stats["separated_lists"]} separated, each able to carry a trailing separator)', file=sys.stderr)
print(f'type slots         : {stats["type_slots"]}', file=sys.stderr)
print(f'bool fields skipped: {stats["skipped_bool"]}', file=sys.stderr)
print(f'experimental nodes : {len(experimental)} ({", ".join(experimental)})', file=sys.stderr)
print(f'lines written      : {len(out) + 1}  -> {os.path.relpath(OUT, ROOT)}', file=sys.stderr)
