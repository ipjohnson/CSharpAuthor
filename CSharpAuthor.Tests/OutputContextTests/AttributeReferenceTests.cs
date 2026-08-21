using Xunit;

namespace CSharpAuthor.Tests.OutputContextTests;

/// <summary>
/// An attribute is written as a type, not as the letters of its name.
/// </summary>
/// <remarks>
/// It used to be written as <c>_attributeType.Name</c> - the simple name, alone - with the namespace
/// declared beside it. Two things went wrong at once. The name went in bare whatever the output mode
/// was, propped up by a directive the writer added itself, which is the same pair of defects holding
/// each other up that the rest of this branch is about. And a name is not a type: everything a type
/// knows about itself that a name does not - its generic arguments, its containing type - was gone
/// by the time it was written.
/// </remarks>
public class AttributeReferenceTests
{
    [Fact]
    public void ThePostfixIsTakenOff()
    {
        Assert.Equal("Marker", Written(TypeDefinition.Get("Sample", "MarkerAttribute")));
    }

    [Fact]
    public void ANameThatIsOnlyThePostfixKeepsIt()
    {
        Assert.Equal("Attribute", Written(TypeDefinition.Get("Sample", "Attribute")));
    }

    [Fact]
    public void ANameThatDoesNotEndInThePostfixIsUntouched()
    {
        Assert.Equal("Marker", Written(TypeDefinition.Get("Sample", "Marker")));
    }

    [Fact]
    public void AGenericAttributeKeepsItsArguments()
    {
        var attributeType = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition,
            "Sample",
            "MarkerAttribute",
            new[] { TypeDefinition.Get(typeof(int)) });

        Assert.Equal("Marker<int>", Written(attributeType));
    }

    [Fact]
    public void AGenericAttributeIsQualifiedWithItsArguments()
    {
        var attributeType = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition,
            "Sample",
            "MarkerAttribute",
            new[] { TypeDefinition.Get("Other", "Payload") });

        Assert.Equal(
            "global::Sample.Marker<global::Other.Payload>",
            Written(attributeType, TypeOutputMode.Global));
    }

    /// <summary>
    /// Whatever the type knows how to write, the attribute writes. Nothing here rebuilds the name
    /// out of its parts, so a type model that learns to write more keeps working.
    /// </summary>
    [Fact]
    public void ItWritesWhateverTheTypeWrites()
    {
        Assert.Equal("Outer.Inner", Written(new NestedTypeStub("Sample", "Outer.InnerAttribute")));
    }

    [Fact]
    public void TheDeclaredNameKeepsThePostfixSoAnAliasCanNameIt()
    {
        var reference = new AttributeTypeReference(TypeDefinition.Get("Sample", "MarkerAttribute"));

        Assert.Equal("MarkerAttribute", reference.Name);
        Assert.Equal("Sample", reference.Namespace);
    }

    /// <summary>An attribute is aliased like anything else when its written name is contested.</summary>
    [Fact]
    public void ACollidingAttributeIsAliasedToTheTypeThatExists()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var classDefinition = file.AddClass("Service");

        classDefinition.AddAttribute(TypeDefinition.Get("First", "MarkerAttribute"));
        classDefinition.AddMethod("Handle").AddAttribute(TypeDefinition.Get("Second", "MarkerAttribute"));

        var context = new OutputContext();

        file.WriteOutput(context);

        var output = context.Output();

        // The alias has to name the type that exists, postfix and all.
        Assert.Contains("using SecondMarker = Second.MarkerAttribute;", output);
        Assert.Contains("[SecondMarker]", output);
    }

    private static string Written(ITypeDefinition attributeType, TypeOutputMode mode = TypeOutputMode.ShortName)
    {
        var builder = new System.Text.StringBuilder();

        new AttributeTypeReference(attributeType).WriteTypeName(builder, mode);

        return builder.ToString();
    }

    /// <summary>
    /// A type whose written name is not its <c>Name</c>, standing in for a nested type until the
    /// type model writes one.
    /// </summary>
    private sealed class NestedTypeStub : ITypeDefinition
    {
        private readonly string _written;

        public NestedTypeStub(string ns, string written)
        {
            Namespace = ns;
            _written = written;
        }

        public TypeDefinitionEnum TypeDefinitionEnum => TypeDefinitionEnum.ClassDefinition;
        public bool IsNullable => false;
        public bool IsArray => false;
        public string Name => "InnerAttribute";
        public string Namespace { get; }
        public System.Collections.Generic.IEnumerable<string> KnownNamespaces
        {
            get { yield return Namespace; }
        }
        public System.Collections.Generic.IReadOnlyList<ITypeDefinition> TypeArguments =>
            System.Array.Empty<ITypeDefinition>();

        public void WriteTypeName(System.Text.StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName)
        {
            if (typeOutputMode == TypeOutputMode.Global)
            {
                builder.Append("global::").Append(Namespace).Append('.');
            }
            else if (typeOutputMode == TypeOutputMode.FullName)
            {
                builder.Append(Namespace).Append('.');
            }

            builder.Append(_written);
        }

        public System.Collections.Generic.IReadOnlyList<int> ArrayRanks => System.Array.Empty<int>();
        public System.Collections.Generic.IReadOnlyList<bool> NullableAnnotations => new[] { false };
        public ITypeDefinition? ContainingType => null;

        public ITypeDefinition MakeNullable(bool nullable = true) => this;
        public ITypeDefinition MakeArray() => this;
        public ITypeDefinition MakeArray(int rank) => this;
        public int CompareTo(ITypeDefinition? other) => ReferenceEquals(this, other) ? 0 : -1;
    }
}
