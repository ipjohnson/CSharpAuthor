// Structural equivalence. What this treats as "the same tree" IS what the headline
// percentage means, so it is stated in one place and nowhere else.
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RoundTrip;

public static class Compare
{
    /// <summary>
    /// The definition, verbatim:
    ///
    ///   Two trees are equivalent when SyntaxNode.IsEquivalentTo(other, topLevel: false)
    ///   is true. That is Roslyn's green-node comparison: same node kinds, same shape,
    ///   same token text - INCLUDING the exact source text of every identifier and
    ///   literal (0x1F is not 31, @class is not class, 1.5f is not 1.5) - and EXCLUDING
    ///   all trivia.
    ///
    /// What that means it does NOT test:
    ///   - whitespace, indentation, line breaks and blank lines
    ///   - comments and XML documentation
    ///   - preprocessor directives (#region, #pragma, #nullable, #if): Roslyn models
    ///     these as trivia, and the importer never sees them
    ///
    /// topLevel: true was rejected. It ignores method bodies, initialisers and every
    /// expression, which would turn the measurement into a declaration-header check and
    /// inflate the number to the point of being worthless.
    /// </summary>
    public static bool Equivalent(SyntaxNode a, SyntaxNode b) => a.IsEquivalentTo(b, topLevel: false);

    /// <summary>
    /// A second, fully explainable verdict, reported alongside the first as a cross-check:
    /// the two trees produce the same sequence of node kinds and the same sequence of
    /// (token kind, token text). It is trivia-blind for the same reason as the primary.
    ///
    /// It exists because Roslyn's IsEquivalentTo rejects a small number of trees on which
    /// every node kind, every token and every child-pair comparison agree - see the
    /// "node differs though all children are equivalent" rows in the histogram. Reporting
    /// both bounds that discrepancy instead of hiding it, and the headline stays on the
    /// stricter of the two.
    /// </summary>
    public static bool TokenAndKindEquivalent(SyntaxNode a, SyntaxNode b)
    {
        var ta = a.DescendantTokens().ToList();
        var tb = b.DescendantTokens().ToList();
        if (ta.Count != tb.Count) return false;
        for (var i = 0; i < ta.Count; i++)
            if (ta[i].RawKind != tb[i].RawKind || ta[i].Text != tb[i].Text) return false;

        var na = a.DescendantNodes().ToList();
        var nb = b.DescendantNodes().ToList();
        if (na.Count != nb.Count) return false;
        for (var i = 0; i < na.Count; i++)
            if (na[i].RawKind != nb[i].RawKind) return false;

        return true;
    }

    /// <summary>
    /// Locate where two trees diverge, for the bucket (c) histogram.
    ///
    /// The locator uses the verdict function itself at every level - descend into the first
    /// child pair that IsEquivalentTo rejects - so the site it names is guaranteed to be a
    /// site the verdict actually cares about. A hand-rolled walk would drift from the
    /// verdict and mislabel the histogram.
    /// </summary>
    public static void Diff(SyntaxNode a, SyntaxNode b, ImportReport report, int limit = 8)
    {
        var found = 0;
        Locate(a, b, report, ref found, limit);
        if (found == 0)
            report.Add(Bucket.Structure, a.Kind().ToString(),
                "IsEquivalentTo rejected the tree but no child pair reproduces it");
    }

    private static void Locate(SyntaxNode a, SyntaxNode b, ImportReport report, ref int found, int limit)
    {
        if (found >= limit) return;

        if (a.RawKind != b.RawKind)
        {
            report.Add(Bucket.Structure, a.Kind().ToString(), $"became {b.Kind()}");
            found++;
            return;
        }

        var ca = a.ChildNodesAndTokens().ToList();
        var cb = b.ChildNodesAndTokens().ToList();

        if (ca.Count != cb.Count)
        {
            report.Add(Bucket.Structure, a.Kind().ToString(),
                $"{ca.Count} children in, {cb.Count} out ({Summary(ca)} -> {Summary(cb)})");
            found++;
            return;
        }

        var before = found;
        for (var i = 0; i < ca.Count && found < limit; i++)
        {
            var x = ca[i];
            var y = cb[i];

            if (x.IsNode && y.IsNode)
            {
                if (!x.AsNode()!.IsEquivalentTo(y.AsNode()!, topLevel: false))
                    Locate(x.AsNode()!, y.AsNode()!, report, ref found, limit);
                continue;
            }

            if (x.IsToken && y.IsToken)
            {
                var tx = x.AsToken();
                var ty = y.AsToken();
                if (tx.RawKind == ty.RawKind && tx.Text == ty.Text && tx.IsMissing == ty.IsMissing)
                    continue;
                report.Add(Bucket.Structure, a.Kind().ToString(),
                    $"token '{Show(tx)}' became '{Show(ty)}'");
                found++;
                continue;
            }

            report.Add(Bucket.Structure, a.Kind().ToString(),
                $"child {i} is {Describe(x)} in, {Describe(y)} out");
            found++;
        }

        // Every child pair agrees, yet the parent does not: the difference is in the node
        // itself. Naming it is still useful - it says exactly which grammar node to look at.
        if (found == before)
        {
            report.Add(Bucket.Structure, a.Kind().ToString(),
                $"node differs though all {ca.Count} children are equivalent " +
                $"[{string.Join(" ", ca.Select(Describe))}] IN <{Head(a)}> OUT <{Head(b)}>");
            found++;
        }
    }

    private static string Show(SyntaxToken t) => t.IsMissing ? $"<missing {t.Kind()}>" : t.Text;

    private static string Describe(SyntaxNodeOrToken x) =>
        x.IsToken ? "token " + Show(x.AsToken()) : x.Kind().ToString();

    private static string Head(SyntaxNode n)
    {
        var t = n.ToString().Replace("\r", " ").Replace("\n", " ");
        return t.Length > 60 ? t.Substring(0, 60) + "..." : t;
    }

    private static string Summary(List<SyntaxNodeOrToken> items)
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < items.Count && i < 6; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(Describe(items[i]));
        }
        if (items.Count > 6) sb.Append(" ...");
        return sb.ToString();
    }
}
