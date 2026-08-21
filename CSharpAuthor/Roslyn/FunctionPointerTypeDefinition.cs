using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor.Roslyn;

/// <summary>
/// A function pointer type — <c>delegate*&lt;int, void&gt;</c>,
/// <c>delegate* unmanaged[Cdecl]&lt;int, int&gt;</c>.
/// </summary>
/// <remarks>
/// The return type is the last argument in the syntax, which is why it is written after the
/// parameters rather than before them. The calling convention is carried as written because the set
/// is open-ended and the tokens are not types. Emitting one requires C# 9 and an unsafe context; the
/// conversion produces the type either way rather than losing it, and what a target language version
/// permits is a question for the writer.
/// </remarks>
public sealed class FunctionPointerTypeDefinition : ITypeDefinition
{
    private readonly IReadOnlyList<ITypeDefinition> _parameterTypes;
    private int? _hashCode;

    public FunctionPointerTypeDefinition(
        IReadOnlyList<ITypeDefinition> parameterTypes,
        ITypeDefinition returnType,
        string? callingConvention = null)
    {
        _parameterTypes = parameterTypes ?? throw new ArgumentNullException(nameof(parameterTypes));
        ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
        CallingConvention = callingConvention;
    }

    public IReadOnlyList<ITypeDefinition> ParameterTypes => _parameterTypes;

    public ITypeDefinition ReturnType { get; }

    /// <summary>The text between <c>delegate*</c> and the argument list, such as <c>unmanaged[Cdecl]</c>.</summary>
    public string? CallingConvention { get; }

    public TypeDefinitionEnum TypeDefinitionEnum => TypeDefinitionEnum.ClassDefinition;

    public bool IsNullable => false;

    public bool IsArray => false;

    public string Name => "delegate*";

    public string Namespace => "";

    public IEnumerable<string> KnownNamespaces
    {
        get
        {
            foreach (var parameterType in _parameterTypes)
            {
                foreach (var knownNamespace in parameterType.KnownNamespaces)
                {
                    yield return knownNamespace;
                }
            }

            foreach (var knownNamespace in ReturnType.KnownNamespaces)
            {
                yield return knownNamespace;
            }
        }
    }

    public IReadOnlyList<ITypeDefinition> TypeArguments => _parameterTypes;

    public void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName)
    {
        builder.Append("delegate*");

        if (!string.IsNullOrEmpty(CallingConvention))
        {
            builder.Append(' ');
            builder.Append(CallingConvention);
        }

        builder.Append('<');

        foreach (var parameterType in _parameterTypes)
        {
            parameterType.WriteTypeName(builder, typeOutputMode);
            builder.Append(", ");
        }

        ReturnType.WriteTypeName(builder, typeOutputMode);

        builder.Append('>');
    }

    public ITypeDefinition MakeNullable(bool nullable = true)
    {
        return this;
    }

    public ITypeDefinition MakeArray()
    {
        return new ArrayTypeDefinition(this);
    }

    /// <summary>
    /// The rank of each array wrapping this type, outermost first. Empty: this type is not an array.
    /// </summary>
    /// <remarks>
    /// Present so the bridge's types satisfy the type model's array-rank contract without a change
    /// at merge time.
    /// </remarks>
    public IReadOnlyList<int> ArrayRanks => Array.Empty<int>();

    /// <summary>The type this one is declared inside. A function pointer is declared inside nothing, which is what its symbol reports.</summary>
    public ITypeDefinition? ContainingType => null;

    /// <summary>An array of this type with the given rank.</summary>
    public ITypeDefinition MakeArray(int rank)
    {
        return new ArrayTypeDefinition(this, rank);
    }

    public int CompareTo(ITypeDefinition? other)
    {
        if (ReferenceEquals(other, null))
        {
            return 1;
        }

        if (other is not FunctionPointerTypeDefinition functionPointer)
        {
            var otherCompare = string.Compare(ToString(), other.ToString(), StringComparison.Ordinal);

            return otherCompare != 0 ? otherCompare : -1;
        }

        return string.Compare(ToString(), functionPointer.ToString(), StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is FunctionPointerTypeDefinition functionPointer && CompareTo(functionPointer) == 0;
    }

    public override int GetHashCode()
    {
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        return _hashCode ??= ToString().GetHashCode();
    }

    public override string ToString()
    {
        var builder = new StringBuilder();

        WriteTypeName(builder, TypeOutputMode.FullName);

        return builder.ToString();
    }
}
