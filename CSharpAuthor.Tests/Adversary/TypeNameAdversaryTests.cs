using System;
using System.Collections.Generic;
using Xunit;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// How else can a type be written? Arrays of arrays, arrays of ranks, nullable elements, nested
/// types, and generic types nested inside generic types.
/// </summary>
public class TypeNameAdversaryTests
{
    private const string NestedTypes = @"
namespace Probe
{
    public class Outer { public class Inner { public class Deepest { } } }
    public class OuterG<T> { public class InnerG<U> { } }
}
";

    /// <summary>
    /// <c>string?[]</c> is an array of nullable strings. <c>string[]?</c> is a nullable array of
    /// non-null strings. They are different types, both compile, and the library can only write the
    /// second - so a caller asking for the first is given the other one with no indication.
    /// </summary>
    [Fact]
    public void NullableElementArray_WritesQuestionBeforeBrackets()
    {
        var type = TypeDefinition.Get(typeof(string)).MakeNullable().MakeArray();

        Assert.Equal("string?[]", Emit.TypeName(type));
    }

    /// <summary>
    /// The same defect asked as a compile question rather than a string question, so it cannot be
    /// satisfied by agreeing with the current output.
    /// </summary>
    [Fact]
    public void NullableElementArray_AcceptsNullElement()
    {
        var type = TypeDefinition.Get(typeof(string)).MakeNullable().MakeArray();

        RoslynAssert.MemberCompiles(
            "public void M(" + Emit.TypeName(type) + " values) { values[0] = null; }",
            warningsAsErrors: "CS8625");
    }

    [Fact(Skip = "ADVERSARY GAP: same fixed ? / [] order on a type parameter - T?[] is written as T[]?")]
    public void NullableElementArray_OnTypeParameter()
    {
        var type = new TypeParameterDefinition("T", isNullable: true, isArray: true);

        Assert.Equal("T?[]", Emit.TypeName(type));
    }

    [Fact]
    public void NullableElementArray_OnGenericType()
    {
        var type = new GenericTypeDefinition(
                TypeDefinitionEnum.ClassDefinition, "System.Collections.Generic", "List",
                new[] { TypeDefinition.Get(typeof(int)) })
            .MakeNullable()
            .MakeArray();

        Assert.Equal("List<int>?[]", Emit.TypeName(type));
    }

    /// <summary>
    /// An array of a constructed generic. The arity marker from the CLR name is written straight
    /// into the source, and the rank is written twice.
    /// </summary>
    [Fact]
    public void ArrayOfConstructedGeneric()
    {
        var type = TypeDefinition.Get(typeof(List<int>[]));

        Assert.Equal("List<int>[]", Emit.TypeName(type));

        RoslynAssert.MemberCompiles("public " + Emit.TypeName(type) + " Field;");
    }

    /// <summary>
    /// A generic type nested in a generic type. The outer type's arguments are merged into the
    /// inner's list, producing the name of a type that may well exist and is not this one.
    /// </summary>
    [Fact]
    public void GenericNestedInGeneric()
    {
        var type = TypeDefinition.Get(typeof(OuterG<int>.InnerG<string>));

        Assert.Equal("Outer" + "G<int>.InnerG<string>", Emit.TypeName(type));
    }

    /// <summary>
    /// The same, asked of the compiler: whatever is written has to name a type that exists.
    /// </summary>
    [Fact]
    public void GenericNestedInGeneric_NamesATypeThatExists()
    {
        var type = TypeDefinition.Get(typeof(OuterG<int>.InnerG<string>));

        RoslynAssert.MemberCompiles(
            "public Probe." + Emit.TypeName(type) + " Field;",
            preamble: NestedTypes);
    }

    /// <summary>
    /// §7 already records that a nested type loses its container. This states it as a compile
    /// question so the fix is verified rather than asserted.
    /// </summary>
    [Fact]
    public void NestedType_KeepsItsContainer()
    {
        var type = TypeDefinition.Get(typeof(Outer.Inner));

        RoslynAssert.MemberCompiles(
            "public Probe." + Emit.TypeName(type) + " Field;",
            preamble: NestedTypes);
    }

    [Fact]
    public void DeeplyNestedType_KeepsItsContainers()
    {
        var type = TypeDefinition.Get(typeof(Outer.Inner.Deepest));

        Assert.Equal("Outer.Inner.Deepest", Emit.TypeName(type));
    }

    /// <summary>
    /// A nested type reports the namespace of its outermost container, which is the namespace that
    /// has to be imported - so the using is right even though the name is not. Guarding it means a
    /// fix for the name cannot quietly break the using.
    /// </summary>
    [Fact]
    public void NestedType_ImportsItsContainersNamespace()
    {
        var type = TypeDefinition.Get(typeof(Outer.Inner));

        Assert.Contains("CSharpAuthor.Tests.Adversary", type.KnownNamespaces);
    }

    [Fact]
    public void MultiDimensionalArray()
    {
        var type = TypeDefinition.Get(typeof(int[,]));

        Assert.Equal("int[,]", Emit.TypeName(type));
    }

    [Fact(Skip = "ADVERSARY GAP: typeof(int[,][]) emits Int32[][,][] - a jagged array of rank-2 arrays gains a third rank and loses the keyword")]
    public void JaggedArrayOfMultiDimensional()
    {
        var type = TypeDefinition.Get(typeof(int[,][]));

        Assert.Equal("int[][,]", Emit.TypeName(type));
    }

    [Fact(Skip = "ADVERSARY GAP: typeof(int[][,]) emits Int32[,][][] - same defect with the ranks in the other order")]
    public void MultiDimensionalArrayOfJagged()
    {
        var type = TypeDefinition.Get(typeof(int[][,]));

        Assert.Equal("int[,][]", Emit.TypeName(type));
    }

    [Fact]
    public void MakeArrayTwice()
    {
        var type = TypeDefinition.Get(typeof(int)).MakeArray().MakeArray();

        Assert.Equal("int[][]", Emit.TypeName(type));
    }

    /// <summary>
    /// The §5 case, all of it at once: a generic closed over a generic closed over a nullable value
    /// type, made nullable, then jagged twice.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: emits List<Dictionary<string,int?>>[]? - one rank lost to the bool IsArray, and the ? on the wrong side of the brackets")]
    public void TheWholeShape()
    {
        var dictionary = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "System.Collections.Generic", "Dictionary",
            new[] { TypeDefinition.Get(typeof(string)), TypeDefinition.Get(typeof(int)).MakeNullable() });

        var list = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "System.Collections.Generic", "List",
            new ITypeDefinition[] { dictionary });

        var type = list.MakeNullable().MakeArray().MakeArray();

        Assert.Equal("List<Dictionary<string, int?>>?[][]", Emit.TypeName(type));
    }

    /// <summary>
    /// A nested generic argument reaches output correctly as long as no array or nullable is in
    /// play, and both inner namespaces are imported. Unskipped, so a fix for the cases above has to
    /// keep this working.
    /// </summary>
    [Fact]
    public void NestedGenericArguments_Compile()
    {
        var dictionary = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "System.Collections.Generic", "Dictionary",
            new[] { TypeDefinition.Get(typeof(string)), TypeDefinition.Get(typeof(int)).MakeNullable() });

        var list = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "System.Collections.Generic", "List",
            new ITypeDefinition[] { dictionary });

        RoslynAssert.MemberCompiles("public " + Emit.TypeName(list) + " Field;");
    }

    /// <summary>
    /// <c>int?</c> is written as <c>Nullable&lt;int&gt;</c>, which is the same type spelled the long
    /// way. It compiles, so it is a style question rather than a defect, and the guard records that
    /// it compiles today.
    /// </summary>
    [Fact]
    public void NullableValueType_CompilesAsNullableOfT()
    {
        var type = TypeDefinition.Get(typeof(int?));

        Assert.Equal("Nullable<int>", Emit.TypeName(type));

        RoslynAssert.MemberCompiles("public " + Emit.TypeName(type) + " Field;");
    }

    /// <summary>
    /// An open generic, for <c>typeof(List&lt;&gt;)</c>.
    /// </summary>
    [Fact]
    public void OpenGenericType_Compiles()
    {
        var closed = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "System.Collections.Generic", "Dictionary",
            new[] { TypeDefinition.Get(typeof(string)), TypeDefinition.Get(typeof(int)) });

        var open = closed.MakeOpenType();

        RoslynAssert.StatementCompiles("var t = typeof(" + Emit.TypeName(open) + ");");
    }

    /// <summary>
    /// A jagged array written the one way the model can express it. Unskipped: this is the shape
    /// consumers rely on and a rank fix must not disturb it.
    /// </summary>
    [Fact]
    public void SingleRankArray_Compiles()
    {
        RoslynAssert.MemberCompiles(
            "public " + Emit.TypeName(TypeDefinition.Get(typeof(string)).MakeArray()) + " Field;");
    }

    /// <summary>
    /// Global mode on a nested type. It writes <c>global::</c> and the namespace, then the wrong
    /// name - so the qualification machinery is correct and the name is not.
    /// </summary>
    [Fact]
    public void NestedType_InGlobalMode()
    {
        var type = TypeDefinition.Get(typeof(Outer.Inner));

        Assert.Equal(
            "global::CSharpAuthor.Tests.Adversary.Outer.Inner",
            Emit.TypeName(type, TypeOutputMode.Global));
    }
}

public class Outer
{
    public class Inner
    {
        public class Deepest
        {
        }
    }
}

public class OuterG<T>
{
    public class InnerG<U>
    {
    }
}
