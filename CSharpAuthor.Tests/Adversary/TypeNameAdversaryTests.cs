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

    /// <summary>
    /// Both shapes on a type parameter, and which constructor reaches which.
    /// </summary>
    /// <remarks>
    /// The <c>isNullable</c> flag is the <em>outer</em> annotation, so with <c>isArray</c> it gives
    /// <c>T[]?</c> - a nullable array. That is deliberate and documented on the constructor;
    /// <c>MakeArrayOfNullable</c> is how a caller asks for <c>T?[]</c>, an array of nullable
    /// elements. The placeholder this replaces asserted that the first spelling produced the
    /// second, which would have made the two shapes unreachable from one another.
    /// </remarks>
    [Fact]
    public void NullableElementArray_OnTypeParameter()
    {
        Assert.Equal(
            "T[]?",
            Emit.TypeName(new TypeParameterDefinition("T", isNullable: true, isArray: true)));

        Assert.Equal(
            "T?[]",
            Emit.TypeName(new TypeParameterDefinition("T").MakeArrayOfNullable()));
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

    /// <summary>
    /// <c>int[,][]</c> is a rank-2 array whose element type is <c>int[]</c> - the first bracket
    /// group is the outer rank, so the name has to be written back in the order it was read.
    /// </summary>
    /// <remarks>
    /// The placeholder this replaces expected <c>int[][,]</c>, which names the other type, and its
    /// skip reason described the 1.x output (<c>Int32[][,][]</c> - a third rank, and the keyword
    /// lost). Both were fixed by the 2.0 type-model rewrite. The compile check is what stops this
    /// being satisfied by agreeing with whatever is emitted.
    /// </remarks>
    [Fact]
    public void JaggedArrayOfMultiDimensional()
    {
        var type = TypeDefinition.Get(typeof(int[,][]));

        Assert.Equal("int[,][]", Emit.TypeName(type));

        RoslynAssert.MemberCompiles(
            "public " + Emit.TypeName(type) + " Field; public void M() { Field = new int[1,1][]; }");
    }

    /// <summary>The mirror: <c>int[][,]</c> is a rank-1 array whose element type is <c>int[,]</c>.</summary>
    [Fact]
    public void MultiDimensionalArrayOfJagged()
    {
        var type = TypeDefinition.Get(typeof(int[][,]));

        Assert.Equal("int[][,]", Emit.TypeName(type));

        RoslynAssert.MemberCompiles(
            "public " + Emit.TypeName(type) + " Field; public void M() { Field = new int[1][,]; }");
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
    /// <remarks>
    /// The structural half of the old skip reason - a rank lost to the <c>bool IsArray</c>, and the
    /// <c>?</c> on the wrong side of the brackets - is fixed. What remains is cosmetic: no space
    /// after the comma separating generic arguments, where Roslyn's normalised form has one. The
    /// compile check pins the meaning; the string check pins the current spelling.
    /// </remarks>
    [Fact]
    public void TheWholeShape()
    {
        var dictionary = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "System.Collections.Generic", "Dictionary",
            new[] { TypeDefinition.Get(typeof(string)), TypeDefinition.Get(typeof(int)).MakeNullable() });

        var list = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "System.Collections.Generic", "List",
            new ITypeDefinition[] { dictionary });

        var type = list.MakeNullable().MakeArray().MakeArray();

        Assert.Equal("List<Dictionary<string,int?>>?[][]", Emit.TypeName(type));

        RoslynAssert.MemberCompiles("public " + Emit.TypeName(type) + " Field;");
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
