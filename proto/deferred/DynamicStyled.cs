#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpAuthor;
using Deferred;

namespace DynamicTest;

// ---------------------------------------------------------------------------
// Same payload and same requirement change as DynamicV2, but now the output has
// to honour the host project's style: indent char and width, brace placement,
// line ending, and block-vs-file-scoped namespace.
//
// The tree version takes these as a StyleOptions object and its BODY DOES NOT
// CHANGE AT ALL. This file exists to measure what the StringBuilder version
// costs to reach the same capability.
// ---------------------------------------------------------------------------
public sealed class SbStyle
{
    public char IndentChar = ' ';
    public int IndentWidth = 4;
    public string NewLine = "\n";
    public bool KAndR = false;            // brace on the same line as the header
    public bool FileScopedNamespace = true;
}

public static class DynamicStyled
{
    public static string WithStringBuilder(List<Svc> model, SbStyle st)
    {
        var fieldsBuf = new StringBuilder();
        var factoriesBuf = new StringBuilder();
        var bodyBuf = new StringBuilder();

        var usings = new SortedSet<string>();
        var emittedFactories = new HashSet<string>();
        var loggerEmitted = false;

        var anyConditional = model.Any(s => s.Conditional);
        usings.Add("Microsoft.Extensions.DependencyInjection");
        if (anyConditional) usings.Add("Acme.Runtime");

        // (a) indent must now honour char AND width
        string Ind(int d) => new string(st.IndentChar, d * st.IndentWidth);

        // (b) a block namespace shifts EVERYTHING inside it one level deeper,
        //     so every depth in the file becomes relative to a base.
        var b = st.FileScopedNamespace ? 0 : 1;

        // (c) brace placement can no longer be emitted independently of the
        //     header line — K&R has to join them, Allman has to break them.
        //     Every open brace in the file goes through here.
        void Open(StringBuilder sb, int depth, string header)
        {
            if (st.KAndR)
                sb.Append(Ind(depth)).Append(header).Append(' ').Append('{').Append(st.NewLine);
            else
                sb.Append(Ind(depth)).Append(header).Append(st.NewLine)
                  .Append(Ind(depth)).Append('{').Append(st.NewLine);
        }
        void Close(StringBuilder sb, int depth) =>
            sb.Append(Ind(depth)).Append('}').Append(st.NewLine);
        void Line(StringBuilder sb, int depth, string text) =>
            sb.Append(Ind(depth)).Append(text).Append(st.NewLine);

        foreach (var svc in model)
        {
            usings.Add(svc.Ns);

            var depth = b + 2;
            if (svc.Conditional && anyConditional)
            {
                Open(bodyBuf, depth, "if (environment.Is(\"prod\"))");
                depth++;

                if (!loggerEmitted)
                {
                    Line(fieldsBuf, b + 1, "private static ILogger _logger;");
                    fieldsBuf.Append(st.NewLine);
                    loggerEmitted = true;
                }

                Open(bodyBuf, depth, "try");
                depth++;
            }

            if (svc.NeedsFactory)
            {
                var factoryName = "Create" + svc.Name;
                if (emittedFactories.Add(factoryName))
                {
                    Open(factoriesBuf, b + 1,
                        $"private static {svc.Name} {factoryName}(IServiceProvider provider)");
                    Line(factoriesBuf, b + 2, $"return new {svc.Name}();");
                    Close(factoriesBuf, b + 1);
                    factoriesBuf.Append(st.NewLine);
                    usings.Add("System");
                }

                Line(bodyBuf, depth,
                    $"services.Add{svc.Lifetime}(typeof({svc.Name}), {factoryName});");
            }
            else
            {
                Line(bodyBuf, depth, $"services.Add{svc.Lifetime}(typeof({svc.Name}));");
            }

            if (svc.Conditional && anyConditional)
            {
                depth--;
                Close(bodyBuf, depth);
                Open(bodyBuf, depth, "catch (Exception e)");
                Line(bodyBuf, depth + 1, "_logger.Error(e);");
                Close(bodyBuf, depth);
                usings.Add("System");
                depth--;
                Close(bodyBuf, depth);
            }
        }

        Line(bodyBuf, b + 2, "return services;");

        var sb2 = new StringBuilder();
        foreach (var u in usings)
            sb2.Append("using ").Append(u).Append(';').Append(st.NewLine);
        sb2.Append(st.NewLine);

        // (d) the namespace form changes the file's whole shape, not just a line
        if (st.FileScopedNamespace)
        {
            sb2.Append("namespace Acme.Generated;").Append(st.NewLine).Append(st.NewLine);
        }
        else
        {
            Open(sb2, 0, "namespace Acme.Generated");
        }

        Open(sb2, b, "public static class Registrations");
        sb2.Append(fieldsBuf);
        sb2.Append(factoriesBuf);

        var sig = "public static IServiceCollection Register(IServiceCollection services"
                + (anyConditional ? ", IModuleEnvironment environment" : "") + ")";
        Open(sb2, b + 1, sig);
        sb2.Append(bodyBuf);
        Close(sb2, b + 1);
        Close(sb2, b);

        if (!st.FileScopedNamespace) Close(sb2, 0);

        return sb2.ToString();
    }

    // The tree version for the same capability. Body identical to DynamicV2 —
    // only the StyleOptions handed to the writer differs.
    public static string WithTree(List<Svc> model, StyleOptions style) =>
        RenderTree(model, style);

    static string RenderTree(List<Svc> model, StyleOptions style)
    {
        // DynamicV2.WithTree already builds the tree; we only re-render it.
        // Reproduced here only because that method renders internally.
        return DynamicV2.WithTreeStyled(model, style);
    }
}
