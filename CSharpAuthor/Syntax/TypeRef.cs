#nullable enable

namespace CSharpAuthor.Syntax;

/// <summary>
/// Whatever can stand in a <c>TypeSyntax</c> slot: an unrendered <see cref="ITypeDefinition"/>,
/// or a grammar node that builds a type out of other nodes.
/// </summary>
/// <remarks>
/// <para>
/// Every type-shaped field in the grammar takes one of these. The <see cref="ITypeDefinition"/>
/// case is the deferral point - the reference is carried unrendered all the way to
/// serialisation, where <see cref="IOutputContext.Write(ITypeDefinition)"/> decides between a
/// short name, a full name and <c>global::</c>, and records the namespace it implies. Nothing
/// here ever calls <c>AddImportNamespace</c>, and nothing here ever turns a type into text.
/// </para>
/// <para>
/// The node case exists because the generator emits <c>ArrayType</c>, <c>NullableType</c>,
/// <c>GenericName</c> and friends, and they would be unreachable if a type slot only accepted
/// <see cref="ITypeDefinition"/>.
/// </para>
/// </remarks>
#if CSHARPAUTHOR_PUBLIC_SYNTAX
public
#endif
readonly struct TypeRef
{
    private readonly ITypeDefinition? _definition;
    private readonly IType? _node;

    private TypeRef(ITypeDefinition? definition, IType? node)
    {
        _definition = definition;
        _node = node;
    }

    /// <summary>Nothing at all - an absent optional type.</summary>
    public static TypeRef None => default;

    /// <summary>True when this slot is empty.</summary>
    public bool IsEmpty => _definition == null && _node == null;

    /// <summary>The type reference, still unrendered, when this holds one.</summary>
    public ITypeDefinition? Definition => _definition;

    /// <summary>The type node, when this holds one.</summary>
    public IType? Node => _node;

    public static implicit operator TypeRef(TypeDefinition? definition) =>
        definition == null ? default : new TypeRef(definition, null);

    public static implicit operator TypeRef(GenericTypeDefinition? definition) =>
        definition == null ? default : new TypeRef(definition, null);

    /// <summary>Convenience: a bare name becomes a namespace-less type reference.</summary>
    public static implicit operator TypeRef(string? name) =>
        string.IsNullOrEmpty(name) ? default : new TypeRef(new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", name!, false), null);

    // C# forbids a user-defined conversion from an interface, so IType cannot have one.
    // The concrete type nodes a caller actually writes get one each, which is what keeps
    // `new MethodDeclaration(new PredefinedType("void"), ...)` from needing a wrapper.

    public static implicit operator TypeRef(PredefinedType? node) => Of(node);

    public static implicit operator TypeRef(ArrayType? node) => Of(node);

    public static implicit operator TypeRef(NullableType? node) => Of(node);

    public static implicit operator TypeRef(PointerType? node) => Of(node);

    public static implicit operator TypeRef(TupleType? node) => Of(node);

    public static implicit operator TypeRef(IdentifierName? node) => Of(node);

    public static implicit operator TypeRef(GenericName? node) => Of(node);

    public static implicit operator TypeRef(QualifiedName? node) => Of(node);

    public static implicit operator TypeRef(AliasQualifiedName? node) => Of(node);

    /// <summary>Wrap an <see cref="ITypeDefinition"/>. Cannot be an implicit operator - it is an interface.</summary>
    public static TypeRef Of(ITypeDefinition? definition) =>
        definition == null ? default : new TypeRef(definition, null);

    /// <summary>Wrap a type node. Cannot be an implicit operator - it is an interface.</summary>
    public static TypeRef Of(IType? node) =>
        node == null ? default : new TypeRef(null, node);

    internal void Write(SyntaxWriter writer)
    {
        if (_definition != null)
        {
            // Invariant 1: the one channel a type may reach output through.
            writer.Context.Write(_definition);
        }
        else
        {
            _node?.WriteOutput(writer.Context);
        }
    }
}
