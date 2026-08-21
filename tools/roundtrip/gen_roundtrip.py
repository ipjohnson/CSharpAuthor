#!/usr/bin/env python3
"""
V2-HANDOFF.md 9(b) - the round-trip importer generator.

This is proto/grammar/gen_all.py's field walk, INVERTED. gen_all.py walks the fields of
a node in Syntax.xml order and writes each one to an IOutputContext; this walks the same
fields in the same order and READS each one off the Roslyn node. Everything about which
fields exist, in what order, and how each field's type maps onto the node layer is taken
from the same inputs gen_all.py uses, so the two cannot drift apart by hand-editing.

It generates two importers, against two node layers:

  proto  the node layer exactly as committed in proto/grammar/Nodes.cs + Hand.cs.
         This is the HEADLINE measurement: it measures the emitter that exists.
         Because nodes.json drops every Syntax.xml node carrying
         SkipConvenienceFactories="true" (10 of 250, including ClassDeclarationSyntax),
         the missing classes are generated here into NodesSkipped.g.cs by gen_all.py's
         IDENTICAL algorithm - otherwise the corpus could not be imported at all and the
         measurement would be vacuous. If nodes.json ever gains them, this file empties
         itself automatically.

  rt     a complete 250-node layer generated from Syntax.xml by the same field walk with
         four deliberate, enumerated differences (see LAYER_RT_DELTA below). This is the
         DIAGNOSTIC ceiling: what the generated-grammar thesis achieves once the node
         model can represent an absent optional token. It is NOT the product's number.

Usage:
    python3 tools/roundtrip/gen_roundtrip.py --repo <path-to-csharpauthor-checkout>

Writes tools/roundtrip/CSharpAuthor.RoundTrip/Generated/*.g.cs (in THIS repository -
never in the repository under test) and prints a machine-readable summary to stderr.
"""

import argparse, importlib.util, json, os, re, sys
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.join(HERE, 'CSharpAuthor.RoundTrip', 'Generated')

LAYER_RT_DELTA = [
    "every SyntaxToken field carries its source text and is nullable; an absent optional "
    "token is null and is not emitted (proto hard-codes single-kind tokens and emits them "
    "unconditionally)",
    "SyntaxList<SyntaxToken> becomes List<string> (proto makes it List<ISyntax>)",
    "the category interfaces follow Roslyn's real AbstractNode hierarchy, so a type is an "
    "expression (proto flattens to 18 sibling interfaces, so it is not)",
    "all 250 Syntax.xml nodes are generated (proto generates 233)",
]

# ---------------------------------------------------------------------------
# Syntax.xml -> the same node model nodes.json holds.
# ---------------------------------------------------------------------------

def collect_fields(elem):
    """Fields in document order, descending into <Choice>/<Sequence>.

    Roslyn wraps mutually exclusive tails - a method's `{ body }` versus `=> expr;` - in
    <Choice>. They are still fields, still in emit order, and nodes.json flattens them
    exactly this way; missing the recursion silently drops the body and the semicolon from
    every method, constructor, operator and accessor in the grammar.
    """
    for child in elem:
        if child.tag == 'Field':
            yield child
        elif child.tag in ('Choice', 'Sequence'):
            for f in collect_fields(child):
                yield f


def parse_syntax_xml(path):
    root = ET.parse(path).getroot()
    abstract, concrete, predefined = {}, {}, set()
    order = []
    for c in root:
        if c.tag == 'PredefinedNode':
            predefined.add(c.get('Name'))
            continue
        if c.tag not in ('Node', 'AbstractNode'):
            continue
        name = c.get('Name')
        entry = {
            'name': name,
            'base': c.get('Base'),
            'abstract': c.tag == 'AbstractNode',
            'skip_convenience': c.get('SkipConvenienceFactories') == 'true',
            'fields': [
                {
                    'name': f.get('Name'),
                    'type': f.get('Type'),
                    'opt': f.get('Optional') == 'true',
                    'kinds': [k.get('Name') for k in f.findall('Kind')],
                }
                for f in collect_fields(c)
            ],
        }
        if entry['abstract']:
            abstract[name] = entry
        else:
            concrete[name] = entry
            order.append(name)
    # chain: every ancestor up to and including the last named base
    everything = dict(abstract)
    everything.update(concrete)
    for e in everything.values():
        chain, b = [], e['base']
        while b:
            chain.append(b)
            b = everything.get(b, {}).get('base')
            if b in (None, ''):
                break
        e['chain'] = chain
    return abstract, concrete, predefined, order


# ---------------------------------------------------------------------------
# gen_all.py's rules, verbatim. Do not "improve" these - they define the layer
# under test, and the whole point is that the importer mirrors them exactly.
# ---------------------------------------------------------------------------

CATS = ('ExpressionSyntax', 'StatementSyntax', 'PatternSyntax', 'MemberDeclarationSyntax',
        'TypeSyntax', 'VariableDesignationSyntax', 'SwitchLabelSyntax', 'QueryClauseSyntax',
        'InterpolatedStringContentSyntax', 'CollectionElementSyntax', 'BaseTypeSyntax',
        'DirectiveTriviaSyntax', 'XmlNodeSyntax', 'XmlAttributeSyntax', 'CrefSyntax',
        'TypeParameterConstraintSyntax', 'MemberCrefSyntax', 'SelectOrGroupClauseSyntax')

HAND = {'IdentifierNameSyntax', 'LiteralExpressionSyntax', 'InterpolatedStringTextSyntax',
        'XmlTextSyntax', 'OmittedTypeArgumentSyntax', 'OmittedArraySizeExpressionSyntax',
        'BadDirectiveTriviaSyntax', 'XmlProcessingInstructionSyntax'}

DEFERRED_TYPE_FIELDS = ('TypeSyntax', 'NameSyntax', 'SimpleNameSyntax', 'IdentifierNameSyntax')

KEYWORDS = set('''base this object operator default ref out in params new is event using while for if else
lock checked static class string int bool byte long short void return switch case try catch finally throw
do goto fixed unsafe var when where from select let join on equals by into with yield partial global type
value name double float decimal char sbyte uint ulong ushort nint nuint delegate enum struct interface
namespace public private protected internal abstract sealed virtual override readonly const volatile
extern explicit implicit unchecked sizeof typeof stackalloc as null true false'''.split())

CS_KEYWORDS_FULL = KEYWORDS | set('''byte char do finally for foreach goto lock out ref sizeof stackalloc
throw try typeof unchecked unsafe volatile while'''.split())


def safe(name):
    lo = name[0].lower() + name[1:]
    return '@' + lo if lo in KEYWORDS else lo


def prop_safe(name):
    """A property name that cannot collide with a C# keyword."""
    return '@' + name if name in CS_KEYWORDS_FULL else name


class Layer:
    """Everything that differs between the proto layer and the rt layer."""

    def __init__(self, kind, nodes, abstract, tokens, ns, spacing='gen_all'):
        self.spacing = spacing          # 'gen_all' | 'identifier-aware'
        self.kind = kind                # 'proto' | 'rt'
        self.nodes = nodes              # list of node entries, in grammar order
        self.byname = {n['name']: n for n in nodes}
        self.abstract = abstract
        self.tokens = tokens
        self.ns = ns
        self.classname = {}
        used = set()
        for n in nodes:
            if kind == 'proto' and n['name'] in HAND:
                continue
            cls = n['name'].replace('Syntax', '')
            if cls in used:
                cls += 'Node'
            used.add(cls)
            self.classname[n['name']] = cls

    # -- interfaces -------------------------------------------------------
    def root_iface(self):
        return 'ISyntax' if self.kind == 'proto' else 'ISyntaxNode'

    def iface(self, n):
        if self.kind == 'proto':
            for c in n['chain']:
                if c in CATS:
                    return 'I' + c.replace('Syntax', '')
            return 'ISyntax'
        b = n['base']
        return self.iface_name_for_abstract(b)

    def iface_name_for_abstract(self, name):
        if name in self.abstract:
            return 'I' + name.replace('Syntax', '') + 'Node'
        return self.root_iface()

    def iface_for_typename(self, t):
        if self.kind == 'proto':
            if t in self.byname:
                return self.iface(self.byname[t])
            if t in CATS:
                return 'I' + t.replace('Syntax', '')
            return 'ISyntax'
        if t in self.abstract:
            return self.iface_name_for_abstract(t)
        if t in self.byname:
            return self.classname[t]
        return self.root_iface()

    def all_ifaces(self):
        if self.kind == 'proto':
            return sorted({self.iface(n) for n in self.nodes} - {'ISyntax'})
        return None

    # -- the field walk ---------------------------------------------------
    def walk(self, n):
        """Yield (role, field, extra) in Syntax.xml order. Identical traversal to
        gen_all.py; role names what gen_all.py does with the field."""
        for f in n['fields']:
            ft, kinds = f['type'], f['kinds']
            if ft == 'SyntaxToken':
                if self.kind == 'proto' and len(kinds) == 1 and kinds[0] in self.tokens:
                    yield ('fixedtoken', f, self.tokens[kinds[0]])
                else:
                    yield ('valuetoken', f, None)
                continue
            if ft.startswith('SeparatedSyntaxList') or ft.startswith('SyntaxList'):
                inner = ft[ft.index('<') + 1:-1]
                sep = ft.startswith('Separated')
                if inner == 'SyntaxToken':
                    yield ('tokenlist', f, sep)
                else:
                    yield ('list', f, (self.iface_for_typename(inner), sep))
                continue
            if ft in DEFERRED_TYPE_FIELDS:
                yield ('typedef', f, None)
                continue
            yield ('node', f, self.iface_for_typename(ft))

    def ctor_params(self, n):
        """gen_all.py's constructor signature, in order."""
        out = []
        for role, f, extra in self.walk(n):
            lo = safe(f['name'])
            if role == 'fixedtoken':
                continue
            if role == 'valuetoken':
                if self.kind == 'rt':
                    continue                      # rt makes every token a settable nullable prop
                out.append(('string', lo, f, role, extra))
            elif role in ('list', 'tokenlist'):
                continue
            elif role == 'typedef':
                if not f['opt']:
                    out.append(('ITypeDefinition', lo, f, role, extra))
            else:
                if not f['opt']:
                    out.append((extra, lo, f, role, extra))
        return out


# ---------------------------------------------------------------------------
# Node-class emission (gen_all.py's body, parameterised by Layer).
# ---------------------------------------------------------------------------

def emit_node_classes(layer, nodes, header, namespace, with_prelude):
    w = [].append
    out = []
    w = out.append
    w("// <auto-generated> " + header)
    w("// Generated by tools/roundtrip/gen_roundtrip.py from Roslyn's Syntax.xml.")
    w("// Field order in the grammar IS emit order; that is the whole trick.")
    w("#nullable enable")
    w("using System.Collections.Generic;")
    w("using CSharpAuthor;")
    w("")
    w(f"namespace {namespace};")
    w("")
    if with_prelude:
        w(f"public interface {layer.root_iface()} : IOutputComponent {{ }}")
        if layer.kind == 'rt':
            for name, a in sorted(layer.abstract.items()):
                base = layer.iface_name_for_abstract(a['base'])
                w(f"public interface {layer.iface_name_for_abstract(name)} : {base} {{ }}")
        w("")
        if layer.spacing == 'gen_all':
            w("/// <summary>gen_all.py's spacing rule, copied verbatim - the one thing the")
            w("/// grammar does not encode. Word-like tokens need separation from an adjacent")
            w("/// identifier; punctuation does not.</summary>")
            w("public static class Tk")
            w("{")
            w("    public static void W(IOutputContext c, string? t)")
            w("    {")
            w("        if (string.IsNullOrEmpty(t)) return;")
            w("        var word = char.IsLetter(t![0]);")
            w("        if (word && c.LastCharacter is char p &&")
            w("            (char.IsLetterOrDigit(p) || p == '_' || p == ')' || p == ']' || p == '}'))")
            w("            c.WriteSpace();")
            w("        c.Write(t);")
            w("        if (word) c.WriteSpace();")
            w("    }")
            w("")
            w("    public static void T(IOutputContext c, ITypeDefinition? t)")
            w("    {")
            w("        if (t != null) c.Write(t);   // gen_all.py writes types without any spacing")
            w("    }")
            w("}")
            w("")
        else:
            w("/// <summary>gen_all.py's rule with two corrections the corpus forced:")
            w("/// an identifier may start with '_' or '@', and a type name written through")
            w("/// IOutputContext.Write(ITypeDefinition) is word-like on both sides. Without")
            w("/// them `string _x` emits as `string_x`.</summary>")
            w("public static class Tk")
            w("{")
            w("    private static bool WordStart(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '@';")
            w("    private static bool WordEnd(char c) => char.IsLetterOrDigit(c) || c == '_';")
            w("")
            w("    public static void W(IOutputContext c, string? t)")
            w("    {")
            w("        if (string.IsNullOrEmpty(t)) return;")
            w("        var word = WordStart(t![0]);")
            w("        if (word && c.LastCharacter is char p &&")
            w("            (WordEnd(p) || p == ')' || p == ']' || p == '}'))")
            w("            c.WriteSpace();")
            w("        c.Write(t);")
            w("        if (char.IsLetterOrDigit(t[t.Length - 1]) || t[t.Length - 1] == '_') c.WriteSpace();")
            w("    }")
            w("")
            w("    public static void T(IOutputContext c, ITypeDefinition? t)")
            w("    {")
            w("        if (t == null) return;")
            w("        if (c.LastCharacter is char p && WordEnd(p)) c.WriteSpace();")
            w("        c.Write(t);")
            w("        c.WriteSpace();")
            w("    }")
            w("}")
            w("")

    for n in nodes:
        cls = layer.classname[n['name']]
        it = layer.iface(n)
        props, assign, body = [], [], []
        ctor = [f'{t} {lo}' for (t, lo, _f, _r, _e) in layer.ctor_params(n)]
        ctor_fields = {lo for (_t, lo, _f, _r, _e) in layer.ctor_params(n)}

        for role, f, extra in layer.walk(n):
            fn, lo = prop_safe(f['name']), safe(f['name'])
            if role == 'fixedtoken':
                body.append(f'        Tk.W(outputContext, {json.dumps(extra)});')
            elif role == 'valuetoken':
                if layer.kind == 'proto':
                    props.append(f'    public string {fn} {{ get; set; }}')
                    assign.append(f'        {fn} = {lo};')
                else:
                    props.append(f'    public string? {fn} {{ get; set; }}')
                body.append(f'        Tk.W(outputContext, {fn});')
            elif role == 'tokenlist':
                if layer.kind == 'proto':
                    et, sep = layer.root_iface(), extra
                    props.append(f'    public List<{et}> {fn} {{ get; }} = new();')
                    joiner = '            if (i > 0) outputContext.Write(", ");\n' if sep else ''
                    body.append(f'        for (var i = 0; i < {fn}.Count; i++)\n        {{\n{joiner}            {fn}[i].WriteOutput(outputContext);\n        }}')
                else:
                    sep = extra
                    props.append(f'    public List<string> {fn} {{ get; }} = new();')
                    joiner = '            if (i > 0) outputContext.Write(", ");\n' if sep else ''
                    body.append(f'        for (var i = 0; i < {fn}.Count; i++)\n        {{\n{joiner}            Tk.W(outputContext, {fn}[i]);\n        }}')
            elif role == 'list':
                et, sep = extra
                props.append(f'    public List<{et}> {fn} {{ get; }} = new();')
                joiner = '            if (i > 0) outputContext.Write(", ");\n' if sep else ''
                body.append(f'        for (var i = 0; i < {fn}.Count; i++)\n        {{\n{joiner}            {fn}[i].WriteOutput(outputContext);\n        }}')
            elif role == 'typedef':
                props.append(f'    public ITypeDefinition? {fn} {{ get; set; }}')
                if lo in ctor_fields:
                    assign.append(f'        {fn} = {lo};')
                if layer.kind == 'rt':
                    body.append(f'        Tk.T(outputContext, {fn});')
                else:
                    body.append(f'        if ({fn} != null) outputContext.Write({fn});')
            else:
                props.append(f'    public {extra}? {fn} {{ get; set; }}')
                if lo in ctor_fields:
                    assign.append(f'        {fn} = {lo};')
                body.append(f'        {fn}?.WriteOutput(outputContext);')

        w(f'public sealed class {cls} : {it}')
        w('{')
        for p in props:
            w(p)
        if props:
            w('')
        if ctor:
            w(f'    public {cls}({", ".join(ctor)})')
            w('    {')
            for a in assign:
                w(a)
            w('    }')
        else:
            w(f'    public {cls}() {{ }}')
        w('')
        w('    public void AddUsingNamespace(string ns) { }')
        w('')
        w('    public void WriteOutput(IOutputContext outputContext)')
        w('    {')
        for b in body:
            w(b)
        w('    }')
        w('}')
        w('')
    return '\n'.join(out)


# ---------------------------------------------------------------------------
# Importer emission - the inversion.
# ---------------------------------------------------------------------------

def emit_importer(layer, nodes, cls_name, node_ns, hand_map, chain_depth, readable):
    out = []
    w = out.append
    root = layer.root_iface()
    w("// <auto-generated> The Syntax.xml field walk, inverted.")
    w("// gen_all.py writes each field to an IOutputContext, in grammar order. This reads")
    w("// each field off the Roslyn node, in the same grammar order, into the same class.")
    w("// Generated by tools/roundtrip/gen_roundtrip.py. Do not edit by hand.")
    w("#nullable enable")
    w("using System.Collections.Generic;")
    w("using CSharpAuthor;")
    w("using Microsoft.CodeAnalysis;")
    w("using Microsoft.CodeAnalysis.CSharp;")
    w("using R = Microsoft.CodeAnalysis.CSharp.Syntax;")
    w(f"using G = {node_ns};")
    w("")
    w("namespace RoundTrip;")
    w("")
    w(f"public sealed partial class {cls_name} : ImporterBase")
    w("{")
    w(f"    public {cls_name}(ImportReport report, TypeImportMode typeMode) : base(report, typeMode) {{ }}")
    w("")
    w(f"    public G.{root}? Import(SyntaxNode? __node)")
    w("    {")
    w("        if (__node == null) return null;")
    w("        switch (__node)")
    w("        {")
    # most-derived first, so a base-class case can never shadow a derived one
    ordered = sorted(nodes, key=lambda n: -chain_depth.get(n['name'], 0))
    for n in ordered:
        name = n['name']
        if name in hand_map:
            w(f"            case R.{name} __n: return {hand_map[name]};")
            continue
        w(f"            case R.{name} __n: return Import_{layer.classname[name]}(__n);")
    w("            default:")
    w("                Report.Unsupported(__node.Kind().ToString(), \"no importer case for this node kind\");")
    w("                return null;")
    w("        }")
    w("    }")
    w("")

    for n in nodes:
        name = n['name']
        if name in hand_map:
            continue
        cls = layer.classname[name]
        w(f"    private G.{root}? Import_{cls}(R.{name} __n)")
        w("    {")
        params = layer.ctor_params(n)
        ctor_lo = {lo for (_t, lo, _f, _r, _e) in params}
        # 1. read every constructor argument off the Roslyn node, in grammar order
        args = []
        for (t, lo, f, role, extra) in params:
            fn = f['name']
            var = '__a' + lo.lstrip('@')
            if not readable(name, f):
                # Grammar the referenced parser does not have (Syntax.xml runs ahead of the
                # package) or a non-syntax field such as DirectiveTrivia.IsActive.
                args.append('null')
                continue
            if role == 'valuetoken':
                w(f"        var {var} = __n.{fn}.Text;")
                args.append(var)
            elif role == 'typedef':
                w(f"        var {var} = ImportType(__n.{fn}, \"{name}.{fn}\");")
                args.append(var + '!')
            else:
                w(f"        var {var} = As<G.{extra}>(Import(__n.{fn}), __n.{fn}, \"{name}.{fn}\");")
                args.append(var + '!')
        w(f"        var __r = new G.{cls}({', '.join(args)});")
        # 2. everything the constructor did not take
        for role, f, extra in layer.walk(n):
            fn, lo = f['name'], safe(f['name'])
            pn = prop_safe(fn)
            if role == 'fixedtoken':
                continue
            if lo in ctor_lo or not readable(name, f):
                continue
            if role == 'valuetoken':
                w(f"        __r.{pn} = __n.{fn}.RawKind == 0 ? null : __n.{fn}.Text;")
            elif role == 'tokenlist':
                if layer.kind == 'proto':
                    w(f"        foreach (var __t in __n.{fn}) __r.{pn}.Add(Word(__t.Text));")
                else:
                    w(f"        foreach (var __t in __n.{fn}) __r.{pn}.Add(__t.Text);")
            elif role == 'list':
                et, _sep = extra
                w(f"        foreach (var __e in __n.{fn}) {{ var __v = As<G.{et}>(Import(__e), __e, \"{name}.{fn}\"); if (__v != null) __r.{pn}.Add(__v); }}")
            elif role == 'typedef':
                w(f"        __r.{pn} = ImportType(__n.{fn}, \"{name}.{fn}\");")
            else:
                w(f"        __r.{pn} = As<G.{extra}>(Import(__n.{fn}), __n.{fn}, \"{name}.{fn}\");")
        w("        return __r;")
        w("    }")
        w("")
    w("}")
    return '\n'.join(out)


def emit_type_model(multi_rank):
    """The one piece that has to track the type model in the checkout under test.

    7 is actively replacing ITypeDefinition's single IsArray bool with an ArrayRanks list.
    Which of the two this harness compiles against decides whether `int[,][]` is a gap or a
    pass, so it is decided by looking at the checkout rather than by assumption - otherwise
    the measurement reports the type model this tool was written against.
    """
    out = []
    w = out.append
    w("// <auto-generated> Generated by tools/roundtrip/gen_roundtrip.py from the type model")
    w("// found in the checkout under test. Do not edit by hand.")
    w("#nullable enable")
    w("using System.Collections.Generic;")
    w("using CSharpAuthor;")
    w("")
    w("namespace RoundTrip;")
    w("")
    w("internal static class TypeModel")
    w("{")
    w(f"    /// <summary>ITypeDefinition carries ArrayRanks in this checkout: {multi_rank}.</summary>")
    w(f"    public const bool MultiRankArrays = {'true' if multi_rank else 'false'};")
    w("")
    w("    /// <summary>Wrap <paramref name=\"element\"/> in the given array ranks, outermost")
    w("    /// first - the order C# writes them. Null when the model cannot hold that shape.</summary>")
    w("    public static ITypeDefinition? Array(ITypeDefinition element, IReadOnlyList<int> ranks, out string reason)")
    w("    {")
    w("        reason = \"\";")
    w("        if (ranks.Count == 0) return element;")
    if multi_rank:
        w("        // MakeArray puts the new array on the OUTSIDE, so apply innermost first.")
        w("        var result = element;")
        w("        for (var i = ranks.Count - 1; i >= 0; i--) result = result.MakeArray(ranks[i]);")
        w("        return result;")
    else:
        w("        // One bool cannot tell int[] from int[][] from int[,] - 7's defect list.")
        w("        if (ranks.Count != 1)")
        w("        {")
        w("            reason = $\"array with {ranks.Count} rank specifiers - ITypeDefinition.IsArray is one bool\";")
        w("            return null;")
        w("        }")
        w("        if (ranks[0] != 1)")
        w("        {")
        w("            reason = $\"array of rank {ranks[0]} - ITypeDefinition.IsArray is one bool\";")
        w("            return null;")
        w("        }")
        w("        if (element.IsArray)")
        w("        {")
        w("            reason = \"jagged array - MakeArray() on an array drops a rank\";")
        w("            return null;")
        w("        }")
        w("        return element.MakeArray();")
    w("    }")
    w("}")
    return '\n'.join(out)


def emit_proto_hand(layer, namespace, tk_is_public):
    """The escape the proto layer needs and does not have.

    Hand.cs's Raw implements 4 of the 18 category interfaces, so it cannot stand in for a
    modifier token, an interpolated-string text run, or an omitted type argument. This is
    generated rather than hand-written only because the interface list comes from the
    grammar; the class body is the hand-written part.
    """
    ifaces = ', '.join('G.' + i for i in ['ISyntax'] + layer.all_ifaces())
    out = []
    w = out.append
    w("// <auto-generated> Generated by tools/roundtrip/gen_roundtrip.py.")
    w("#nullable enable")
    w("using CSharpAuthor;")
    w(f"using G = {namespace};")
    w("")
    w("namespace RoundTrip;")
    w("")
    w("/// <summary>A single token, in whichever grammar category the field needs. Uses the")
    w("/// node layer's own spacing rule, so it tracks changes to that policy.</summary>")
    w(f"public sealed class ProtoWord : {ifaces}")
    w("{")
    w("    private readonly string _text;")
    w("    public ProtoWord(string text) => _text = text;")
    w("    public void AddUsingNamespace(string ns) { }")
    if tk_is_public:
        w("    public void WriteOutput(IOutputContext c) => G.Tk.W(c, _text);")
    else:
        w("    public void WriteOutput(IOutputContext c)")
        w("    {")
        w("        // Tk is not visible from here; the rule is copied from gen_all.py.")
        w("        if (string.IsNullOrEmpty(_text)) return;")
        w("        var word = char.IsLetter(_text[0]);")
        w("        if (word && c.LastCharacter is char p &&")
        w("            (char.IsLetterOrDigit(p) || p == '_' || p == ')' || p == ']' || p == '}'))")
        w("            c.WriteSpace();")
        w("        c.Write(_text);")
        w("        if (word) c.WriteSpace();")
        w("    }")
    w("}")
    w("")
    w("public sealed partial class ProtoImporter")
    w("{")
    w("    private static G.ISyntax Word(string text) => new ProtoWord(text);")
    w("}")
    return '\n'.join(out)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--repo', required=True, help='the CSharpAuthor checkout under test')
    ap.add_argument('--nodes', default=None, help='override path to the committed Nodes.cs')
    ap.add_argument('--rt-spacing', default='gen_all', choices=('gen_all', 'identifier-aware'),
                    help="spacing policy for the rt (diagnostic) layer only. 'gen_all' copies "
                         "proto/grammar's rule verbatim; 'identifier-aware' applies the two "
                         "corrections the corpus forces, to quantify what fixing spacing is worth.")
    ap.add_argument('--roslyn-nodes', default=None,
                    help='file listing the *Syntax classes the referenced Roslyn actually has '
                         '(produced by tools/roundtrip/RoslynProbe). Nodes absent from it get no '
                         'importer case - they are grammar the parser cannot produce.')
    args = ap.parse_args()

    repo = os.path.abspath(args.repo)
    grammar = os.path.join(repo, 'proto', 'grammar')
    syntax_xml = os.path.join(grammar, 'Syntax.xml')
    nodes_json = os.path.join(grammar, 'nodes.json')
    tokens_json = os.path.join(grammar, 'tokens.json')
    for p in (syntax_xml, nodes_json, tokens_json):
        if not os.path.exists(p):
            sys.exit(f'missing generator input: {p}')

    tokens = json.load(open(tokens_json))
    json_nodes_raw = json.load(open(nodes_json))
    abstract, concrete, predefined, order = parse_syntax_xml(syntax_xml)

    # nodes.json is not clean: it carries a duplicate entry and a node that is not in
    # Syntax.xml. Both are reported; neither is silently tolerated.
    seen, json_nodes, json_dupes = set(), [], []
    for n in json_nodes_raw:
        if n['name'] in seen:
            json_dupes.append(n['name'])
            continue
        seen.add(n['name'])
        json_nodes.append(n)
    json_phantom = sorted(n['name'] for n in json_nodes if n['name'] not in concrete)

    # The "identical field walk" claim is checked, not asserted: re-derive every node
    # nodes.json holds straight from Syntax.xml and require the two to agree field for
    # field, in order. A mismatch means this generator and gen_all.py would build different
    # classes, and the measurement would be meaningless.
    walk_mismatches = []
    for n in json_nodes:
        mine = concrete.get(n['name'])
        if mine is None:
            continue
        a = [(f['name'], f['type'], f['opt'], tuple(f['kinds'])) for f in n['fields']]
        b = [(f['name'], f['type'], f['opt'], tuple(f['kinds'])) for f in mine['fields']]
        if a != b:
            walk_mismatches.append(n['name'])
    # Not fatal: nodes.json is the input gen_all.py actually consumed, so for the proto
    # layer it stays authoritative and the importer matches Nodes.cs exactly. But the drift
    # is real and is reported - nodes.json is out of step with the committed Syntax.xml.
    walk_mismatch_detail = []
    for name in walk_mismatches:
        a = {f['name']: tuple(f['kinds']) for f in {n['name']: n for n in json_nodes}[name]['fields']}
        b = {f['name']: tuple(f['kinds']) for f in concrete[name]['fields']}
        for k in a:
            if a[k] != b.get(k):
                walk_mismatch_detail.append(
                    f"{name}.{k}: nodes.json kinds {list(a[k])} vs Syntax.xml {list(b.get(k, ()))}")

    # Locate the committed node layer and read its namespace out of the file, so the
    # tool keeps working after the grammar agent promotes it to CSharpAuthor.Syntax.
    nodes_cs = args.nodes or find_nodes_cs(repo)
    proto_ns = read_namespace(nodes_cs) if nodes_cs else 'GeneratedGrammar.Full'

    # ---- proto layer -----------------------------------------------------
    proto_layer = Layer('proto', json_nodes, abstract, tokens, proto_ns)
    json_names = {n['name'] for n in json_nodes}
    skipped = [concrete[name] for name in order if name not in json_names]

    # gen_all.py's algorithm applied to the nodes nodes.json dropped. Same Layer, so
    # literally the same code path - these classes are indistinguishable from the ones
    # in Nodes.cs, which is the only reason including them is honest.
    proto_all = json_nodes + skipped
    proto_layer_full = Layer('proto', proto_all, abstract, tokens, proto_ns)

    skipped_src = emit_node_classes(
        proto_layer_full, [n for n in skipped if n['name'] not in HAND],
        f"{len(skipped)} Syntax.xml nodes that nodes.json drops (SkipConvenienceFactories).",
        proto_ns, with_prelude=False)

    # The nodes the referenced Roslyn actually knows about. Anything in Syntax.xml but not
    # in the parser cannot appear in a parsed tree, so it gets no importer case and is
    # reported as unreachable-at-this-language-version rather than silently counted.
    roslyn_known = None
    if args.roslyn_nodes and os.path.exists(args.roslyn_nodes):
        roslyn_known = {l.strip() for l in open(args.roslyn_nodes) if l.strip()}
    unreachable = sorted(n for n in order if roslyn_known is not None and n not in roslyn_known)

    members_path = (os.path.splitext(args.roslyn_nodes)[0] + '.members.txt') if args.roslyn_nodes else None
    roslyn_members = None
    if members_path and os.path.exists(members_path):
        roslyn_members = {l.strip() for l in open(members_path) if l.strip()}

    node_type_names = set(concrete) | set(abstract) | set(predefined)
    absent_fields = []

    def readable(node_name, field):
        """Can this field actually be read off the referenced Roslyn node?"""
        ft = field['type']
        inner = ft
        m = re.match(r'^(Separated)?SyntaxList<(.+)>$', ft)
        if m:
            inner = m.group(2)
        if inner != 'SyntaxToken' and inner not in node_type_names:
            return False                                    # e.g. DirectiveTrivia.IsActive : bool
        if roslyn_members is not None and f"{node_name}.{field['name']}" not in roslyn_members:
            absent_fields.append(f"{node_name}.{field['name']}")
            return False
        return True

    def reachable(ns_list):
        if roslyn_known is None:
            return ns_list
        return [n for n in ns_list if n['name'] in roslyn_known]

    hand_proto = {
        'IdentifierNameSyntax': 'Word(__n.Identifier.Text)',
        'LiteralExpressionSyntax': 'Word(__n.Token.Text)',
        'InterpolatedStringTextSyntax': 'Word(__n.TextToken.Text)',
        'XmlTextSyntax': 'Word(__n.ToString())',
        'OmittedTypeArgumentSyntax': 'Word("")',
        'OmittedArraySizeExpressionSyntax': 'Word("")',
        'BadDirectiveTriviaSyntax': 'Word(__n.ToString())',
        'XmlProcessingInstructionSyntax': 'Word(__n.ToString())',
    }
    depth = {name: len(e['chain']) for name, e in concrete.items()}
    proto_importer = emit_importer(
        proto_layer_full, reachable(proto_all), 'ProtoImporter', proto_ns, hand_proto, depth, readable)

    # ---- rt layer --------------------------------------------------------
    rt_nodes = [concrete[name] for name in order]
    rt_layer = Layer('rt', rt_nodes, abstract, tokens, 'RoundTrip.Rt', spacing=args.rt_spacing)
    rt_src = emit_node_classes(
        rt_layer, rt_nodes,
        "The complete 250-node layer. Diagnostic ceiling, NOT the shipping emitter.",
        'RoundTrip.Rt', with_prelude=True)
    rt_importer = emit_importer(rt_layer, reachable(rt_nodes), 'RtImporter', 'RoundTrip.Rt', {}, depth, readable)

    # ---- the SHIPPING layer, CSharpAuthor.Syntax --------------------------
    shipping_xml = os.path.join(repo, 'tools', 'grammar', 'Syntax.xml')
    shipping_tokens = os.path.join(repo, 'tools', 'grammar', 'tokens.json')
    shipping_nodes = os.path.join(repo, 'CSharpAuthor', 'Syntax', 'Nodes.g.cs')
    shipping = None
    shipping_count = 0
    if os.path.exists(shipping_xml) and os.path.exists(shipping_nodes):
        spec = importlib.util.spec_from_file_location(
            'gen_shipping', os.path.join(HERE, 'gen_shipping.py'))
        gs = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(gs)
        grammar = gs.Grammar(shipping_xml, shipping_tokens)

        def ship_readable(node_name, field_name):
            if roslyn_members is None:
                return True
            return f'{node_name}.{field_name}' in roslyn_members

        ship_depth = {}
        for n in grammar.concrete:
            d, cur = 0, grammar.concrete[n].get('Base')
            while cur:
                d += 1
                nxt = grammar.concrete.get(cur) or grammar.abstract.get(cur)
                cur = nxt.get('Base') if nxt is not None else None
            ship_depth[n] = d
        ship_names = [n for n in grammar.concrete
                      if roslyn_known is None or n in roslyn_known]
        shipping = gs.emit_importer(grammar, ship_names, ship_readable, ship_depth)
        shipping_count = len(ship_names)

    tk_public = True
    if nodes_cs and os.path.exists(nodes_cs):
        tk_public = 'internal static class Tk' not in open(nodes_cs, errors='replace').read()
    proto_hand = emit_proto_hand(proto_layer_full, proto_ns, tk_public)

    itd = os.path.join(repo, 'CSharpAuthor', 'ITypeDefinition.cs')
    multi_rank = os.path.exists(itd) and 'ArrayRanks' in open(itd, errors='replace').read()
    type_model = emit_type_model(multi_rank)

    os.makedirs(OUT_DIR, exist_ok=True)
    write(os.path.join(OUT_DIR, 'ProtoHand.g.cs'), proto_hand)
    ship_path = os.path.join(OUT_DIR, 'ImporterSyntax.g.cs')
    if shipping is not None:
        write(ship_path, shipping)
    elif os.path.exists(ship_path):
        os.remove(ship_path)
    write(os.path.join(OUT_DIR, 'TypeModel.g.cs'), type_model)
    write(os.path.join(OUT_DIR, 'NodesSkipped.g.cs'), skipped_src)
    write(os.path.join(OUT_DIR, 'ImporterProto.g.cs'), proto_importer)
    write(os.path.join(OUT_DIR, 'NodesRt.g.cs'), rt_src)
    write(os.path.join(OUT_DIR, 'ImporterRt.g.cs'), rt_importer)
    write(os.path.join(OUT_DIR, 'layer.json'), json.dumps({
        'protoNamespace': proto_ns,
        'protoNodesFile': nodes_cs,
        'syntaxXmlNodes': len(order),
        'nodesJsonNodes': len(json_nodes),
        'protoHandWritten': sorted(HAND),
        'nodesJsonDropped': sorted(n['name'] for n in skipped),
        'nodesJsonDuplicateEntries': sorted(json_dupes),
        'nodesJsonNotInSyntaxXml': json_phantom,
        'nodesJsonOutOfStepWithSyntaxXml': walk_mismatch_detail,
        'unreachableAtThisRoslynVersion': unreachable,
        'fieldsAbsentAtThisRoslynVersion': sorted(set(absent_fields)),
        'shippingLayer': shipping is not None,
        'shippingImporterCases': shipping_count,
        'typeModelMultiRankArrays': multi_rank,
        'rtSpacing': args.rt_spacing,
        'rtDelta': LAYER_RT_DELTA + ([] if args.rt_spacing == 'gen_all' else [
            "spacing rule corrected: identifier-start includes '_' and '@', and a type written "
            "through IOutputContext.Write(ITypeDefinition) takes surrounding space"]),
    }, indent=2))

    e = sys.stderr
    print(f"Syntax.xml concrete nodes : {len(order)}", file=e)
    print(f"nodes.json nodes          : {len(json_nodes)}", file=e)
    print(f"dropped by nodes.json     : {len(skipped)} -> {', '.join(sorted(n['name'] for n in skipped))}", file=e)
    print(f"proto node namespace      : {proto_ns}  ({nodes_cs})", file=e)
    print(f"type model ArrayRanks     : {multi_rank}", file=e)
    print(f"shipping importer cases   : {shipping_count if shipping is not None else 'none (CSharpAuthor/Syntax not in this checkout)'}", file=e)
    print(f"proto importer cases      : {len(reachable(proto_all))}", file=e)
    print(f"rt node classes           : {len(rt_nodes)}  (spacing: {args.rt_spacing})", file=e)
    print(f"unreachable (Roslyn)      : {len(unreachable)} -> {', '.join(unreachable) or '-'}", file=e)
    print(f"nodes.json duplicates     : {len(json_dupes)} -> {', '.join(sorted(json_dupes)) or '-'}", file=e)
    print(f"nodes.json not in xml     : {len(json_phantom)} -> {', '.join(json_phantom) or '-'}", file=e)
    print(f"fields absent (Roslyn)    : {len(set(absent_fields))} -> {', '.join(sorted(set(absent_fields))) or '-'}", file=e)
    print(f"nodes.json vs Syntax.xml  : {len(walk_mismatch_detail)} field-kind mismatches"
          f"{' -> ' + '; '.join(walk_mismatch_detail) if walk_mismatch_detail else ''}", file=e)


def write(path, text):
    with open(path, 'w') as f:
        f.write(text if text.endswith('\n') else text + '\n')


def find_nodes_cs(repo):
    candidates = [os.path.join(repo, 'proto', 'grammar', 'Nodes.cs')]
    for base in ('CSharpAuthor', 'proto'):
        for dirpath, _dirs, files in os.walk(os.path.join(repo, base)):
            if 'obj' in dirpath or 'bin' in dirpath:
                continue
            for f in files:
                if f.endswith('.cs'):
                    candidates.append(os.path.join(dirpath, f))
    for c in candidates:
        if not os.path.exists(c):
            continue
        with open(c, 'r', errors='replace') as fh:
            head = fh.read(400)
        if "Generated from Roslyn's Syntax.xml" in head:
            return c
    return None


def read_namespace(path):
    with open(path, 'r', errors='replace') as fh:
        for line in fh:
            m = re.match(r'\s*namespace\s+([A-Za-z0-9_.]+)\s*;', line)
            if m:
                return m.group(1)
            m = re.match(r'\s*namespace\s+([A-Za-z0-9_.]+)\s*$', line)
            if m:
                return m.group(1)
    return 'GeneratedGrammar.Full'


if __name__ == '__main__':
    main()
