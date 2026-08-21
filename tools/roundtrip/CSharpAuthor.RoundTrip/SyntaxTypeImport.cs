// The one hand-written part of the shipping-layer importer: what goes into a TypeRef.
// Everything else about SyntaxImporter is generated from Syntax.xml.
#nullable enable
using CSharpAuthor;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using G = CSharpAuthor.Syntax;

namespace RoundTrip;

public sealed partial class SyntaxImporter
{
    /// <summary>
    /// A type-shaped slot. CSharpAuthor.Syntax.TypeRef accepts either an unrendered
    /// ITypeDefinition - the deferral point of 1, the thing the whole design is built
    /// around - or a grammar type node, which is how ArrayType, TupleType, PointerType and
    /// friends stay reachable at all.
    ///
    /// Which of the two is used is the measurement, so it is a mode rather than a guess:
    ///
    ///   Model     ITypeDefinition only. A shape the type model cannot hold is a failure.
    ///             This measures the type model and the deferral, and nothing else.
    ///   Auto      ITypeDefinition when the model can hold the shape, the grammar type node
    ///             when it cannot. This is the layer used as designed and as a caller would
    ///             use it, so it is the headline.
    ///   Verbatim  the type's source text, carried through. Diagnostic only: it separates
    ///             emitter failures from type-model failures.
    /// </summary>
    private G.TypeRef ImportTypeRef(TypeSyntax? type, string where)
    {
        if (type == null) return G.TypeRef.None;

        if (TypeMode == TypeImportMode.Verbatim)
            return G.TypeRef.Of(TypeDefinition.Get("", Flatten(type)));

        var definition = TryImportType(type, out var reason);
        if (definition != null) return G.TypeRef.Of(definition);

        if (TypeMode == TypeImportMode.Model)
        {
            Report.Unsupported(type.Kind().ToString(), $"{where}: {reason}");
            return G.TypeRef.None;
        }

        // Auto: the type model cannot hold this shape, so build it out of grammar nodes.
        var node = As<G.IType>(Import(type), type, where);
        if (node != null) return G.TypeRef.Of(node);

        Report.Unsupported(type.Kind().ToString(),
            $"{where}: neither the type model nor a grammar node could hold it ({reason})");
        return G.TypeRef.None;
    }
}
