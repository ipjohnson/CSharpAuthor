using System.Linq;
using CSharpAuthor;
using CSharpAuthor.Collections;
using CSharpAuthor.Profiles;
using CSharpAuthor.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpAuthor.Docs.Samples.Generator;

/// <summary>
/// For every partial class marked <c>[Describe]</c>, emits a <c>Describe()</c> method that names
/// each public property and its declared type.
/// </summary>
#region generator
[Generator]
public sealed class DescribeGenerator : IIncrementalGenerator
{
    private const string AttributeName = "Acme.Generated.DescribeAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // The marker attribute, emitted into the compilation so users do not depend on a runtime
        // package for it. CSharpAuthor writes this one too.
        context.RegisterPostInitializationOutput(static postInit =>
            postInit.AddSource("DescribeAttribute.g.cs", EmitAttribute()));

        // One EmitProfile per compilation: the host's .editorconfig decides the formatting, and
        // the host's LangVersion decides what may be emitted.
        var profiles = context.AnalyzerConfigOptionsProvider
            .Combine(context.ParseOptionsProvider)
            .Select(static (pair, _) => EmitProfileRoslynExtensions.ForGeneration(pair.Left, pair.Right));

        // The model is an EquatableArray of plain values, so an edit that does not change it
        // does not re-run the emit. A model holding ImmutableArray compares by reference and
        // silently defeats incremental caching.
        var models = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (attributeContext, _) => DescribeModel.From(attributeContext));

        context.RegisterSourceOutput(
            models.Combine(profiles),
            static (production, pair) => production.AddSource(
                pair.Left.Type.Name + ".Describe.g.cs",
                Emit(pair.Left, pair.Right)));
    }

    private static string Emit(DescribeModel model, EmitProfile profile)
    {
        var file = new CSharpFileDefinition(model.Type.Namespace);

        var declaration = file.AddClass(model.Type.Name);
        declaration.Modifiers |= ComponentModifier.Public | ComponentModifier.Partial;

        var describe = declaration.AddMethod("Describe");
        describe.Modifiers |= ComponentModifier.Public;
        describe.SetReturnType(TypeDefinition.Get(typeof(string)));

        var builderType = TypeDefinition.Get("System.Text", "StringBuilder");
        var builder = describe.Assign(SyntaxHelpers.New(builderType)).ToVar("builder");

        foreach (var property in model.Properties)
        {
            // property.Type is an ITypeDefinition carried out of the semantic model by the
            // bridge. It is still unrendered here, so the qualification mode below decides how
            // it is spelled - including the nullable annotation and any array shape.
            var line = SyntaxHelpers.Add(
                SyntaxHelpers.QuoteString(property.Name + ": "),
                SyntaxHelpers.Property(SyntaxHelpers.TypeOf(property.Type), "Name"),
                SyntaxHelpers.QuoteString("\n"));

            describe.AddIndentedStatement(builder.Invoke("Append", line));
        }

        describe.Return(builder.Invoke("ToString"));

        // Global mode: nothing this file names can be captured by a type the user happens to
        // declare in their own namespace.
        var emitted = ProfileEmitter.Emit(file, profile.With(p => p.TypeMode = TypeOutputMode.Global));

        return emitted.Code;
    }

    private static string EmitAttribute()
    {
        var file = new CSharpFileDefinition("Acme.Generated");

        var attribute = file.AddClass("DescribeAttribute");
        attribute.Modifiers |= ComponentModifier.Internal | ComponentModifier.Sealed;
        attribute.AddBaseType(TypeDefinition.Get(typeof(System.Attribute)));
        attribute.AddAttribute(
            TypeDefinition.Get("System", "AttributeUsage"),
            CodeOutputComponent.Get(TypeDefinition.Get("System", "AttributeTargets"), "Class"));

        var output = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });
        file.WriteOutput(output);

        return output.Output();
    }
}

/// <summary>What the generator needs off the syntax tree, and nothing else.</summary>
internal readonly struct DescribeModel
{
    private DescribeModel(ITypeDefinition type, EquatableArray<DescribedProperty> properties)
    {
        Type = type;
        Properties = properties;
    }

    public ITypeDefinition Type { get; }

    public EquatableArray<DescribedProperty> Properties { get; }

    public static DescribeModel From(GeneratorAttributeSyntaxContext context)
    {
        var symbol = (INamedTypeSymbol)context.TargetSymbol;

        var properties = symbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(static property => property.DeclaredAccessibility == Accessibility.Public)
            .Select(static property => new DescribedProperty(property.Name, property.Type.GetTypeDefinition()))
            .ToArray();

        return new DescribeModel(symbol.GetTypeDefinition(), EquatableArray<DescribedProperty>.From(properties));
    }
}

internal readonly struct DescribedProperty
{
    public DescribedProperty(string name, ITypeDefinition type)
    {
        Name = name;
        Type = type;
    }

    public string Name { get; }

    public ITypeDefinition Type { get; }
}
#endregion
