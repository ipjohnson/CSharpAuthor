using System.Text;
using Xunit;

namespace CSharpAuthor.Tests.TypeDefinitionTests;

/// <summary>
/// A user type declared in the global namespace shares its shape with the predefined keywords -
/// empty namespace, bare name - but not their privilege: <c>int</c> must stay bare because
/// <c>global::int</c> does not compile, while a bare <c>Pet</c> written inside <c>namespace X</c>
/// binds to <c>X.Pet</c> the moment a consumer declares one. That capture is the exact thing
/// <see cref="TypeOutputMode.Global"/> promises cannot happen, and it happened: a generator
/// registered a global-namespace service and the container silently served the same-named type
/// from the generated file's own namespace. Global mode therefore writes the bare qualifier -
/// <c>global::Pet</c> - for every empty-namespace type that is not a predefined keyword.
/// </summary>
/// <remarks>
/// The keyword half of the split is pinned by <c>TypeKeywordTests.TheKeywordIsTheSameInEveryOutputMode</c>;
/// the unbound-generic placeholder (an empty <em>name</em>, which takes no qualifier either) by
/// <c>EmptyGenericArgumentTests</c>. This file pins the user-type half.
/// </remarks>
public class GlobalNamespaceTypeTests
{
    private static ITypeDefinition GlobalPet => TypeDefinition.Get("", "Pet");

    private static string Write(ITypeDefinition type, TypeOutputMode mode)
    {
        var builder = new StringBuilder();

        type.WriteTypeName(builder, mode);

        return builder.ToString();
    }

    [Fact]
    public void GlobalMode_QualifiesAGlobalNamespaceType()
    {
        Assert.Equal("global::Pet", Write(GlobalPet, TypeOutputMode.Global));
    }

    /// <summary>
    /// ShortName stays short and FullName stays the full name - which, for a type in the global
    /// namespace, is the bare name. Only Global mode carries the collision promise.
    /// </summary>
    [Fact]
    public void OtherModes_StayBare()
    {
        Assert.Equal("Pet", Write(GlobalPet, TypeOutputMode.ShortName));
        Assert.Equal("Pet", Write(GlobalPet, TypeOutputMode.FullName));
    }

    /// <summary>
    /// The qualifier survives the transforms, because the check reads the name rather than a flag
    /// a clone could drop.
    /// </summary>
    [Fact]
    public void GlobalMode_QualifiesTransformedGlobalNamespaceTypes()
    {
        Assert.Equal("global::Pet?", Write(GlobalPet.MakeNullable(), TypeOutputMode.Global));
        Assert.Equal("global::Pet[]", Write(GlobalPet.MakeArray(), TypeOutputMode.Global));
    }

    /// <summary>
    /// The same transforms on a keyword stay bare - <c>global::int?</c> would not compile, and a
    /// clone that lost track of being predefined would emit exactly that.
    /// </summary>
    [Fact]
    public void GlobalMode_KeywordTransformsStayBare()
    {
        Assert.Equal("int?", Write(TypeDefinition.Get(typeof(int)).MakeNullable(), TypeOutputMode.Global));
        Assert.Equal("int[]", Write(TypeDefinition.Get(typeof(int)).MakeArray(), TypeOutputMode.Global));
    }

    [Fact]
    public void GlobalMode_QualifiesAGlobalNamespaceGenericAndItsArguments()
    {
        var generic = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "", "Wrapper", new[] { GlobalPet });

        Assert.Equal("global::Wrapper<global::Pet>", Write(generic, TypeOutputMode.Global));
    }

    /// <summary>
    /// The scenario that was silently wrong end to end: a file in a namespace referencing a
    /// global-namespace type. Bare, <c>typeof(Pet)</c> here bound to <c>TestNamespace.Pet</c> in a
    /// consuming project that declared one.
    /// </summary>
    [Fact]
    public void GlobalMode_AFileInANamespaceCannotCaptureAGlobalNamespaceType()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var method = file.AddClass("C").AddMethod("M");

        method.AddIndentedStatement(SyntaxHelpers.TypeOf(GlobalPet));

        var context = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        file.WriteOutput(context);

        Assert.Contains("typeof(global::Pet)", context.Output());
    }

    /// <summary>
    /// A declaration is not a reference: a generic parameter handed in as
    /// <c>TypeDefinition.Get("", "T")</c> - an established consumer shape - introduces <c>T</c>,
    /// and <c>void Go&lt;global::T&gt;()</c> is a syntax error. Declaration sites write the bare
    /// name in every mode; the constraint clause already did.
    /// </summary>
    [Fact]
    public void GlobalMode_GenericParameterDeclarationsStayBare()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var classDefinition = file.AddClass("C");

        classDefinition.AddGenericParameter(TypeDefinition.Get("", "TOuter"));

        var method = classDefinition.AddMethod("Go");

        method.AddGenericParameter(TypeDefinition.Get("", "T"));

        var context = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        file.WriteOutput(context);

        var output = context.Output();

        Assert.Contains("class C<TOuter>", output);
        Assert.Contains("Go<T>()", output);
    }
}
