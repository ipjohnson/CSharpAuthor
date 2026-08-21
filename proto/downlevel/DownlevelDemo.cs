using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// ---------------------------------------------------------------------------
// One version-agnostic tree. The WRITER decides how each node renders for the
// target language version. This is only possible because it is a tree — text
// cannot be downleveled.
// ---------------------------------------------------------------------------

foreach (var (target, lv) in new[]
{
    (Lang.CSharp15, LanguageVersion.Preview),
    (Lang.CSharp12, LanguageVersion.CSharp12),
    (Lang.CSharp8,  LanguageVersion.CSharp8),
})
{
    var src = Render(target);
    Console.WriteLine($"################ tree rendered for {target} ################");
    Console.WriteLine(src);
    Check(target, lv, src);
    Console.WriteLine();
}

// And the failure case: emit the C#15 rendering into a C#8 compilation.
Console.WriteLine("################ C#15 rendering fed to a C#8 compiler ################");
Check(Lang.CSharp8, LanguageVersion.CSharp8, Render(Lang.CSharp15));



// ---------------------------------------------------------------------------
static string Render(Lang target)
{
    var c = new Ctx(target);
    var fileScoped = target >= Lang.CSharp10;

    c.W(fileScoped ? "namespace Gen;\n\n" : "namespace Gen\n{\n");

    c.W("public class Widget\n{\n");
    new InitProp("string", "Name").Write(c);
    c.W("\n    public int[] Sizes = ");
    new ArrayOf("int", "1", "2", "3").Write(c);
    c.W(";\n\n    public string Banner = ");
    new Str("hello \"world\"").Write(c);
    c.W(";\n\n    public string Which = ");
    new NameOf("Widget").Write(c);
    c.W(";\n\n    public System.Text.StringBuilder Buf = ");
    new New("System.Text.StringBuilder", targetTyped: true).Write(c);
    c.W(";\n\n    public string Describe(int n) => ");
    new Classify("n", [("1", "\"one\""), ("2", "\"two\"")]).Write(c);
    c.W(";\n\n    public void Scan(int[][] grid)\n    {\n");
    // A labeled loop needs its label; only the C#15 form consumes it directly.
    c.W(target >= Lang.CSharp15 ? "        outer:\n" : "");
    c.W("        foreach (var row in grid)\n        {\n            foreach (var v in row)\n");
    c.W("                if (v < 0) ");
    new LabeledBreak("outer").Write(c);
    c.W("\n        }\n");
    if (target < Lang.CSharp15) c.W("        outer_done: ;\n");
    c.W("    }\n}\n");

    if (!fileScoped) c.W("}\n");

    var head = new StringBuilder();
    foreach (var p in c.Polyfills)
        head.Append("namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }\n");
    foreach (var n in c.Notes) head.Append("// DOWNLEVEL: " + n + "\n");
    return head + c.Sb.ToString();
}

static bool Check(Lang target, LanguageVersion lv, string src)
{
    var tree = CSharpSyntaxTree.ParseText(src, new CSharpParseOptions(lv));
    var refs = AppDomain.CurrentDomain.GetAssemblies()
        .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
        .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location));
    var comp = CSharpCompilation.Create("d", [tree], refs,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    var errs = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
    Console.WriteLine(errs.Count == 0
        ? $"  COMPILES at {lv}"
        : $"  FAILS at {lv}:");
    foreach (var e in errs.Take(3)) Console.WriteLine($"      {e.Id}: {e.GetMessage()}");
    return errs.Count == 0;
}


enum Lang { CSharp7_3 = 73, CSharp8 = 80, CSharp9 = 90, CSharp10 = 100, CSharp12 = 120, CSharp15 = 150 }

sealed class Ctx
{
    public StringBuilder Sb = new();
    public Lang Target;
    public SortedSet<string> Polyfills = new();
    public List<string> Notes = new();
    public Ctx(Lang t) => Target = t;
    public void W(string s) => Sb.Append(s);
}

interface INode { void Write(Ctx c); }

// --- a collection of values: [1, 2, 3] on C#12+, new[] { 1, 2, 3 } before ---
sealed class ArrayOf(string elementType, params string[] values) : INode
{
    public void Write(Ctx c)
    {
        if (c.Target >= Lang.CSharp12) { c.W("[" + string.Join(", ", values) + "]"); return; }
        c.W($"new {elementType}[] {{ {string.Join(", ", values)} }}");
    }
}

// --- target-typed new: new() on C#9+, new T() before ---
sealed class New(string type, bool targetTyped) : INode
{
    public void Write(Ctx c)
    {
        if (targetTyped && c.Target >= Lang.CSharp9) { c.W("new()"); return; }
        c.W($"new {type}()");
    }
}

// --- raw string: """...""" on C#11+, escaped literal before ---
sealed class Str(string value) : INode
{
    public void Write(Ctx c)
    {
        if (c.Target >= Lang.CSharp12)
        {
            // A raw literal's fence must be LONGER than the longest quote run it
            // contains, and needs padding when the content starts or ends with a
            // quote. Getting this wrong is CS8998 — which is exactly why callers
            // should never hand-roll it.
            var longest = 0; var run = 0;
            foreach (var ch in value)
            { run = ch == '"' ? run + 1 : 0; if (run > longest) longest = run; }
            var fence = new string('"', System.Math.Max(3, longest + 1));
            var pad = (value.StartsWith("\"") || value.EndsWith("\"")) ? " " : "";
            c.W(fence + pad + value + pad + fence);
            return;
        }
        var sb = new StringBuilder("\"");
        foreach (var ch in value)
            sb.Append(ch switch { '"' => "\\\"", '\\' => "\\\\", '\n' => "\\n", _ => ch.ToString() });
        c.W(sb.Append('"').ToString());
    }
}

// --- nameof: literal on C#5, nameof from C#6 ---
sealed class NameOf(string symbol) : INode
{
    public void Write(Ctx c) => c.W(c.Target >= Lang.CSharp8 ? $"nameof({symbol})" : $"\"{symbol}\"");
}

// --- an init-only property. Needs IsExternalInit below C#9 — polyfillable. ---
sealed class InitProp(string type, string name) : INode
{
    public void Write(Ctx c)
    {
        if (c.Target >= Lang.CSharp9)
        {
            if (c.Target < Lang.CSharp10) c.Polyfills.Add("IsExternalInit");
            c.W($"    public {type} {name} {{ get; init; }}\n");
            return;
        }
        c.Notes.Add($"{name}: 'init' unavailable below C#9 — emitted as a settable property, immutability lost");
        c.W($"    public {type} {name} {{ get; set; }}\n");
    }
}

// --- labeled break (C#15) downlevels to goto, which has existed since C#1 ---
sealed class LabeledBreak(string label) : INode
{
    public void Write(Ctx c) =>
        c.W(c.Target >= Lang.CSharp15 ? $"break {label};" : $"goto {label}_done;");
}

// --- switch expression (C#8) -> ternary chain before ---
sealed class Classify(string input, (string, string)[] arms) : INode
{
    public void Write(Ctx c)
    {
        if (c.Target >= Lang.CSharp8)
        {
            c.W($"{input} switch {{ " +
                string.Join(", ", arms.Select(a => $"{a.Item1} => {a.Item2}")) + ", _ => null }");
            return;
        }
        var s = "null";
        foreach (var (test, val) in arms.Reverse()) s = $"{input} == {test} ? {val} : {s}";
        c.W(s);
    }
}

