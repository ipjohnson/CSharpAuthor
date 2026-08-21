using Xunit;

namespace CSharpAuthor.Tests.OutputContextTests;

/// <summary>
/// Two types with the same short name and different namespaces.
/// </summary>
/// <remarks>
/// Importing both namespaces and writing the bare name twice is CS0104: the reference is ambiguous
/// and the file does not compile. Which name is contested is only knowable once the whole file has
/// been written, which is the reason nothing is turned into text before then.
/// </remarks>
public class CollisionAliasTests
{
    [Fact]
    public void TheSecondOfTwoSameNamedTypesIsAliased()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var classDefinition = file.AddClass("Service");

        var method = classDefinition.AddMethod("Handle");
        method.AddParameter(TypeDefinition.Get("First", "Model"), "a");
        method.AddParameter(TypeDefinition.Get("Second", "Model"), "b");

        var output = Write(file);

        Assert.Contains("using First;", output);
        Assert.Contains("using SecondModel = Second.Model;", output);
        Assert.DoesNotContain("using Second;", output);
        Assert.Contains("Handle(Model a, SecondModel b)", output);
    }

    [Fact]
    public void TheAliasIsTakenFromTheNamespaceThatForcedIt()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var classDefinition = file.AddClass("Service");

        var method = classDefinition.AddMethod("Handle");
        method.AddParameter(TypeDefinition.Get("Company.Core", "Widget"), "a");
        method.AddParameter(TypeDefinition.Get("Company.Web.Models", "Widget"), "b");

        var output = Write(file);

        Assert.Contains("using ModelsWidget = Company.Web.Models.Widget;", output);
        Assert.Contains("Handle(Widget a, ModelsWidget b)", output);
    }

    [Fact]
    public void OneTypeUsedTwiceIsNotACollision()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var classDefinition = file.AddClass("Service");

        var method = classDefinition.AddMethod("Handle");
        method.AddParameter(TypeDefinition.Get("First", "Model"), "a");
        method.AddParameter(TypeDefinition.Get("First", "Model"), "b");

        var output = Write(file);

        Assert.Contains("using First;", output);
        Assert.DoesNotContain(" = First.Model;", output);
        Assert.Contains("Handle(Model a, Model b)", output);
    }

    /// <summary>
    /// The name left plain has to be aliased too when the other namespace cannot be dropped.
    /// </summary>
    /// <remarks>
    /// Aliasing only one of the two works because the other namespace stops being imported. When
    /// something else in that namespace is still written by its plain name the import has to stay,
    /// and the bare name is ambiguous again - so neither side keeps it.
    /// </remarks>
    [Fact]
    public void BothAreAliasedWhenNeitherNamespaceCanBeDropped()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var classDefinition = file.AddClass("Service");

        var method = classDefinition.AddMethod("Handle");
        method.AddParameter(TypeDefinition.Get("First", "Model"), "a");
        method.AddParameter(TypeDefinition.Get("Second", "Model"), "b");
        method.AddParameter(TypeDefinition.Get("Second", "Other"), "c");

        var output = Write(file);

        Assert.Contains("using FirstModel = First.Model;", output);
        Assert.Contains("using SecondModel = Second.Model;", output);
        Assert.Contains("using Second;", output);
        Assert.Contains("Handle(FirstModel a, SecondModel b, Other c)", output);
    }

    /// <summary>
    /// A generic cannot be aliased - a using alias names a closed type - so both sides are qualified.
    /// </summary>
    [Fact]
    public void CollidingGenericsAreQualifiedRatherThanAliased()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var classDefinition = file.AddClass("Service");

        var intType = TypeDefinition.Get(typeof(int));

        var method = classDefinition.AddMethod("Handle");
        method.AddParameter(
            new GenericTypeDefinition(TypeDefinitionEnum.ClassDefinition, "First", "Box", new[] { intType }), "a");
        method.AddParameter(
            new GenericTypeDefinition(TypeDefinitionEnum.ClassDefinition, "Second", "Box", new[] { intType }), "b");

        var output = Write(file);

        Assert.DoesNotContain(" = First.Box", output);
        Assert.Contains("Handle(First.Box<int> a, Second.Box<int> b)", output);
    }

    /// <summary>
    /// The same name at two arities does not collide: C# resolves them separately.
    /// </summary>
    [Fact]
    public void TheSameNameAtTwoAritiesIsNotACollision()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var classDefinition = file.AddClass("Service");

        var intType = TypeDefinition.Get(typeof(int));

        var method = classDefinition.AddMethod("Handle");
        method.AddParameter(TypeDefinition.Get("First", "Box"), "a");
        method.AddParameter(
            new GenericTypeDefinition(TypeDefinitionEnum.ClassDefinition, "First", "Box", new[] { intType }), "b");

        var output = Write(file);

        Assert.DoesNotContain(" = First.Box", output);
        Assert.Contains("Handle(Box a, Box<int> b)", output);
    }

    /// <summary>
    /// A generic parameter has no namespace, so it cannot be aliased and does not need to be: it is
    /// in scope by declaration and wins over anything a using brings in. The named type moves.
    /// </summary>
    [Fact]
    public void AGenericParameterKeepsItsNameAndTheTypeMoves()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var classDefinition = file.AddClass("Service");

        var method = classDefinition.AddMethod("Handle");
        method.AddGenericParameter(new TypeParameterDefinition("T"));
        method.AddParameter(new TypeParameterDefinition("T"), "a");
        method.AddParameter(TypeDefinition.Get("Other", "T"), "b");

        var output = Write(file);

        Assert.Contains("using OtherT = Other.T;", output);
        Assert.Contains("Handle<T>(T a, OtherT b)", output);
    }

    /// <summary>
    /// A colliding type nested inside a generic is aliased where it stands.
    /// </summary>
    [Fact]
    public void ACollisionInsideAGenericArgumentIsAliased()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var classDefinition = file.AddClass("Service");

        var method = classDefinition.AddMethod("Handle");
        method.AddParameter(TypeDefinition.Get("First", "Model"), "a");
        method.AddParameter(
            TypeDefinition.IEnumerable(TypeDefinition.Get("Second", "Model")), "b");

        var output = Write(file);

        Assert.Contains("using SecondModel = Second.Model;", output);
        Assert.Contains("IEnumerable<SecondModel> b", output);
    }

    [Fact]
    public void AliasingIsOffWhenTheOptionIsOff()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var classDefinition = file.AddClass("Service");

        var method = classDefinition.AddMethod("Handle");
        method.AddParameter(TypeDefinition.Get("First", "Model"), "a");
        method.AddParameter(TypeDefinition.Get("Second", "Model"), "b");

        var context = new OutputContext(new OutputContextOptions { AliasCollisions = false });

        file.WriteOutput(context);

        var output = context.Output();

        Assert.DoesNotContain(" = Second.Model;", output);
        Assert.Contains("using Second;", output);
        Assert.Contains("Handle(Model a, Model b)", output);
    }

    private static string Write(CSharpFileDefinition file)
    {
        var context = new OutputContext();

        file.WriteOutput(context);

        return context.Output();
    }
}
