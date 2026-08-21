using System;
using System.Collections.Generic;
using System.Text;
using CSharpAuthor;

namespace CSharpAuthor.Docs.Samples;

/// <summary>Samples for docfx/docs/type-model.md.</summary>
public static class TypeModelSamples
{
    /// <summary>Four ways to name a type, and what each one renders as.</summary>
    public static string Constructing()
    {
        #region constructing
        // From reflection. Predefined types come back as C# keywords, not CLR names.
        ITypeDefinition intType = TypeDefinition.Get(typeof(int));

        // By namespace and name, for a type that does not exist yet - the usual case in a
        // generator, where you are naming something you are about to emit.
        ITypeDefinition service = TypeDefinition.Get("Acme.Services", "GreetingService");

        // Closed generics. The arguments are ITypeDefinitions, so they defer too.
        ITypeDefinition listOfService = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition,
            "System.Collections.Generic",
            "List",
            new[] { service });

        // Shapes are applied to a type, not spelled into its name.
        ITypeDefinition jagged = intType.MakeArray().MakeArray();       // int[][]
        ITypeDefinition rectangular = intType.MakeArray(2);             // int[,]
        ITypeDefinition maybeService = service.MakeNullable();          // GreetingService?
        ITypeDefinition nested = TypeDefinition.GetNested(service, "Options");
        #endregion

        var builder = new StringBuilder();

        foreach (var type in new[] { intType, service, listOfService, jagged, rectangular, maybeService, nested })
        {
            var shortName = new StringBuilder();
            var global = new StringBuilder();

            type.WriteTypeName(shortName, TypeOutputMode.ShortName);
            type.WriteTypeName(global, TypeOutputMode.Global);

            builder.Append(shortName).Append("  ->  ").Append(global).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>One tree, two files. This is what deferred rendering buys.</summary>
    public static string OneTreeTwoRenderings()
    {
        #region one-tree-two-renderings
        // Build the tree once. Nothing here decides how a type will be spelled.
        var file = new CSharpFileDefinition("Acme.Reporting");

        var report = file.AddClass("Report");
        report.Modifiers |= ComponentModifier.Public;

        var lines = TypeDefinition.Get(typeof(List<string>));
        report.AddProperty(lines, "Lines").Modifiers |= ComponentModifier.Public;

        var render = report.AddMethod("Render");
        render.Modifiers |= ComponentModifier.Public;
        render.SetReturnType(typeof(string));
        render.Return("string.Join(\", \", Lines)");

        // Render it twice, with two different qualification modes.
        string shortNames = Render(file, TypeOutputMode.ShortName);
        string global = Render(file, TypeOutputMode.Global);

        static string Render(CSharpFileDefinition file, TypeOutputMode mode)
        {
            var output = new OutputContext(new OutputContextOptions { TypeOutputMode = mode });
            file.WriteOutput(output);

            return output.Output();
        }
        #endregion

        return "=== ShortName ===\n" + shortNames + "\n=== Global ===\n" + global;
    }

    /// <summary>The arrays and nullability the type model gets right that string concatenation does not.</summary>
    public static string Shapes()
    {
        #region shapes
        ITypeDefinition intType = TypeDefinition.Get(typeof(int));
        ITypeDefinition stringType = TypeDefinition.Get(typeof(string));

        var shapes = new (string Description, ITypeDefinition Type)[]
        {
            ("a jagged array", intType.MakeArray().MakeArray()),
            ("a rectangular array", intType.MakeArray(2)),
            ("rectangular, then jagged", intType.MakeArray(2).MakeArray()),
            ("a nullable array", stringType.MakeArray().MakeNullable()),
            ("an array of nullables", stringType.MakeArrayOfNullable()),
        };
        #endregion

        var builder = new StringBuilder();

        foreach (var (description, type) in shapes)
        {
            var name = new StringBuilder();
            type.WriteTypeName(name);

            builder.Append(name).Append(new string(' ', Math.Max(1, 14 - name.Length)))
                   .Append("// ").Append(description).Append('\n');
        }

        return builder.ToString();
    }
}
