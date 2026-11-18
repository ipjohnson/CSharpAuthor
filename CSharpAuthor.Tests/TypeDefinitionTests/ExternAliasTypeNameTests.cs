using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using CSharpAuthor.Tests.Adversary;
using Microsoft.CodeAnalysis;
using Xunit;

namespace CSharpAuthor.Tests.TypeDefinitionTests;

/// <summary>
/// <see cref="ITypeDefinitionExtensions.WithExternAlias"/>: a type from an assembly referenced with
/// an extern alias is written <c>Alias::Ns.Type</c> in every mode and derives no using.
/// </summary>
/// <remarks>
/// The files are compiled against <see cref="AliasedVendor"/> and <see cref="GlobalVendor"/>, which
/// both declare <c>Vendor.Widgets.Widget</c>. Each file reads a member only the intended assembly's
/// type has, so a type that lost its alias binds to the other assembly and the read is CS1061.
/// The <c>extern alias V1;</c> directive is put in front by hand until the file can write it.
/// </remarks>
public class ExternAliasTypeNameTests
{
    private const string AliasedSource = @"
namespace Vendor.Widgets
{
    public class Widget
    {
        public int FromV1;

        public class Part { public int PartFromV1; }
    }

    public class Box<T>
    {
        public T Item;
        public int BoxFromV1;
    }
}
";

    private const string GlobalSource = @"
namespace Vendor.Widgets
{
    public class Widget
    {
        public int FromV2;

        public class Part { }
    }

    public class Box<T> { public T Item; }
}

namespace Other
{
    public class Widget { public int FromOther; }
}
";

    private static readonly MetadataReference AliasedVendor = RoslynAssert.Reference("VendorV1", AliasedSource, "V1");

    private static readonly MetadataReference GlobalVendor = RoslynAssert.Reference("VendorV2", GlobalSource);

    private static readonly ITypeDefinition Widget = TypeDefinition.Get("Vendor.Widgets", "Widget");

    private static readonly ITypeDefinition V1Widget = Widget.WithExternAlias("V1");

    private static ITypeDefinition BoxOf(ITypeDefinition argument) =>
        new GenericTypeDefinition(TypeDefinitionEnum.ClassDefinition, "Vendor.Widgets", "Box", new[] { argument });

    private static string Render(CSharpFileDefinition file, TypeOutputMode mode) =>
        "extern alias V1;\n" + Emit.File(file, new OutputContextOptions { TypeOutputMode = mode });

    public static IEnumerable<object[]> Modes() => new[]
    {
        new object[] { TypeOutputMode.ShortName },
        new object[] { TypeOutputMode.FullName },
        new object[] { TypeOutputMode.Global },
    };

    [Theory]
    [MemberData(nameof(Modes))]
    public void MarkedTypeIsWrittenThroughItsAliasInEveryMode(TypeOutputMode mode)
    {
        Assert.Equal("V1::Vendor.Widgets.Widget", Emit.TypeName(V1Widget, mode));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void ShapesKeepWorkingOnAMarkedType(TypeOutputMode mode)
    {
        Assert.Equal("V1::Vendor.Widgets.Widget[]", Emit.TypeName(V1Widget.MakeArray(), mode));
        Assert.Equal("V1::Vendor.Widgets.Widget?", Emit.TypeName(V1Widget.MakeNullable(), mode));
        Assert.Equal("V1::Vendor.Widgets.Widget?[]", Emit.TypeName(V1Widget.MakeArrayOfNullable(), mode));
        Assert.Equal("V1::Vendor.Widgets.Widget[]?", Emit.TypeName(V1Widget.MakeArray().MakeNullable(), mode));
        Assert.Equal("V1::Vendor.Widgets.Widget[,][]", Emit.TypeName(V1Widget.MakeArray().MakeArray(2), mode));
        Assert.Equal("V1::Vendor.Widgets.Widget", Emit.TypeName(V1Widget.MakeNullable().MakeNullable(false), mode));
    }

    [Theory]
    [InlineData(TypeOutputMode.ShortName, "V1::Vendor.Widgets.Box<StringBuilder>[]")]
    [InlineData(TypeOutputMode.FullName, "V1::Vendor.Widgets.Box<System.Text.StringBuilder>[]")]
    [InlineData(TypeOutputMode.Global, "V1::Vendor.Widgets.Box<global::System.Text.StringBuilder>[]")]
    public void TypeArgumentsAreWrittenAsTheyAreForAnUnmarkedType(TypeOutputMode mode, string expected)
    {
        var box = BoxOf(TypeDefinition.Get(typeof(StringBuilder))).WithExternAlias("V1").MakeArray();

        Assert.Equal(expected, Emit.TypeName(box, mode));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void NestedTypeIsWrittenThroughItsContainersAlias(TypeOutputMode mode)
    {
        var throughContainer = TypeDefinition.GetNested(V1Widget, "Part");
        var markedAfterwards = TypeDefinition.GetNested(Widget, "Part").WithExternAlias("V1");

        Assert.Equal("V1::Vendor.Widgets.Widget.Part", Emit.TypeName(throughContainer, mode));
        Assert.Equal("V1::Vendor.Widgets.Widget.Part", Emit.TypeName(markedAfterwards, mode));
        Assert.Equal(throughContainer, markedAfterwards);
    }

    [Fact]
    public void MarkingLeavesTheOriginalUnmarked()
    {
        Assert.Equal("Widget", Widget.GetShortName());
        Assert.Equal("global::Vendor.Widgets.Widget", Emit.TypeName(Widget, TypeOutputMode.Global));
    }

    [Fact]
    public void MarkingAgainReplacesTheAlias()
    {
        Assert.Equal("V2::Vendor.Widgets.Widget", V1Widget.WithExternAlias("V2").GetShortName());
    }

    [Fact]
    public void AKeywordAliasIsEscaped()
    {
        var marked = Widget.WithExternAlias("event");

        Assert.Equal("@event::Vendor.Widgets.Widget", marked.GetShortName());
        Assert.Equal(marked, Widget.WithExternAlias("@event"));

        var file = new CSharpFileDefinition("Sample");
        var holder = file.AddClass("Holder");
        holder.AddProperty(marked, "Value");
        holder.AddMethod("Check").AddCode("_ = Value.FromV1;");

        var output = "extern alias @event;\n" + Emit.File(file);

        RoslynAssert.CompilesWithReferences(
            output, RoslynAssert.Reference("VendorEvent", AliasedSource, "event"), GlobalVendor);
    }

    [Fact]
    public void MarkedAndUnmarkedTypesAreDifferentTypes()
    {
        Assert.NotEqual(Widget, V1Widget);
        Assert.NotEqual(V1Widget, Widget);
        Assert.NotEqual(Widget.GetHashCode(), V1Widget.GetHashCode());
        Assert.NotEqual(0, V1Widget.CompareTo(Widget));
        Assert.NotEqual(TypeDefinitionIdentity.KeyOf(Widget), TypeDefinitionIdentity.KeyOf(V1Widget));
        Assert.NotEqual(Widget.ToString(), V1Widget.ToString());

        Assert.NotEqual(V1Widget, Widget.WithExternAlias("V2"));
        Assert.NotEqual(BoxOf(Widget), BoxOf(Widget).WithExternAlias("V1"));

        var again = TypeDefinition.Get("Vendor.Widgets", "Widget").WithExternAlias("V1");

        Assert.Equal(V1Widget, again);
        Assert.Equal(V1Widget.GetHashCode(), again.GetHashCode());

        var set = new HashSet<ITypeDefinition> { Widget };

        Assert.DoesNotContain(V1Widget, set);
    }

    [Fact]
    public void MarkedTypeKnowsNoNamespaceOfItsOwn()
    {
        Assert.Empty(V1Widget.KnownNamespaces);
        Assert.Empty(TypeDefinition.GetNested(V1Widget, "Part").KnownNamespaces);
        Assert.Equal(new[] { "System.Text" },
            BoxOf(TypeDefinition.Get(typeof(StringBuilder))).WithExternAlias("V1").KnownNamespaces);
    }

    /// <summary>
    /// The aliased assembly is the only reference here that declares <c>Vendor.Widgets</c>, so a
    /// derived <c>using Vendor.Widgets;</c> does not compile (CS0246).
    /// </summary>
    [Theory]
    [MemberData(nameof(Modes))]
    public void NoUsingIsDerivedForAMarkedType(TypeOutputMode mode)
    {
        var file = new CSharpFileDefinition("Sample");
        var holder = file.AddClass("Holder");
        holder.AddProperty(V1Widget, "Value");
        holder.AddProperty(TypeDefinition.GetNested(V1Widget, "Part"), "Piece");
        holder.AddProperty(BoxOf(TypeDefinition.Get(typeof(StringBuilder))).WithExternAlias("V1").MakeArray(), "Boxes");
        holder.AddMethod("Check").AddCode(
            "_ = Value.FromV1;\n_ = Piece.PartFromV1;\n_ = Boxes[0].BoxFromV1;\nglobal::System.Text.StringBuilder item = Boxes[0].Item;");

        var output = Render(file, mode);

        Assert.DoesNotMatch(new Regex(@"^using\s+Vendor", RegexOptions.Multiline), output);
        Assert.DoesNotContain("global::V1", output);
        Assert.DoesNotContain("global::Vendor", output);
        Assert.Contains("V1::Vendor.Widgets.Widget Value", output);
        Assert.Contains("V1::Vendor.Widgets.Widget.Part Piece", output);

        if (mode == TypeOutputMode.ShortName)
        {
            Assert.Contains("using System.Text;", output);
        }

        RoslynAssert.CompilesWithReferences(output, AliasedVendor);
        RoslynAssert.CompilesWithReferences(output, AliasedVendor, GlobalVendor);
    }

    /// <summary>
    /// Both assemblies declare <c>Vendor.Widgets.Widget</c>, so each property binds to the
    /// assembly its name reaches. A marked type that shared the unmarked one's name or using would
    /// bind to the global assembly, and the read of <c>FromV1</c> fails.
    /// </summary>
    [Theory]
    [MemberData(nameof(Modes))]
    public void MarkedAndUnmarkedTypesOfOneNameEachBindToTheirOwnAssembly(TypeOutputMode mode)
    {
        var file = new CSharpFileDefinition("Sample");
        var holder = file.AddClass("Holder");
        holder.AddProperty(V1Widget, "Aliased");
        holder.AddProperty(Widget, "Plain");
        holder.AddMethod("Check").AddCode("_ = Aliased.FromV1;\n_ = Plain.FromV2;");

        var output = Render(file, mode);

        Assert.Contains("V1::Vendor.Widgets.Widget Aliased", output);
        Assert.DoesNotMatch(new Regex(@"^using\s+\w+\s*=", RegexOptions.Multiline), output);

        if (mode == TypeOutputMode.ShortName)
        {
            Assert.Contains("using Vendor.Widgets;", output);
            Assert.Contains("public Widget Plain", output);
        }

        RoslynAssert.CompilesWithReferences(output, AliasedVendor, GlobalVendor);
    }

    /// <summary>
    /// <c>Widget</c> is contested by <c>Vendor.Widgets</c> and <c>Other</c>, so the name plan aliases
    /// one of them. The marked type is not part of that contest: it gets no alias and keeps its
    /// own name.
    /// </summary>
    [Fact]
    public void MarkedTypeTakesNoPartInAShortNameCollision()
    {
        var file = new CSharpFileDefinition("Sample");
        var holder = file.AddClass("Holder");
        holder.AddProperty(Widget, "Plain");
        holder.AddProperty(V1Widget, "Aliased");
        holder.AddProperty(TypeDefinition.Get("Other", "Widget"), "Third");
        holder.AddMethod("Check").AddCode("_ = Aliased.FromV1;\n_ = Plain.FromV2;\n_ = Third.FromOther;");

        var output = Render(file, TypeOutputMode.ShortName);

        Assert.Contains("V1::Vendor.Widgets.Widget Aliased", output);
        Assert.DoesNotContain("= V1::", output);
        Assert.Matches(new Regex(@"^using\s+\w+\s*=", RegexOptions.Multiline), output);

        RoslynAssert.CompilesWithReferences(output, AliasedVendor, GlobalVendor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Two Words")]
    [InlineData("V1::")]
    [InlineData("Vendor.V1")]
    [InlineData("1V")]
    [InlineData("global")]
    [InlineData("@global")]
    public void AnAliasThatIsNotAnIdentifierIsRefused(string alias)
    {
        Assert.Throws<ArgumentException>(() => Widget.WithExternAlias(alias));
    }

    [Fact]
    public void AKeywordTypeIsRefused()
    {
        Assert.Throws<ArgumentException>(() => TypeDefinition.Get(typeof(int)).WithExternAlias("V1"));
    }

    [Fact]
    public void ATypeThisAssemblyCannotCopyIsRefused()
    {
        Assert.Throws<NotSupportedException>(() => new TypeParameterDefinition("T").WithExternAlias("V1"));
    }
}
