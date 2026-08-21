using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// CSharpAuthor.LanguageVersion (the profiles capability enum) used to shadow Roslyn's here: it was
// in the bare CSharpAuthor namespace, this namespace nests under that one, and enclosing-namespace
// members outrank using-aliases, so only a distinct alias name disambiguated. It is in
// CSharpAuthor.Profiles now and this file does not import it, so nothing shadows anything; the
// alias stays because it says which LanguageVersion is meant.
using RoslynLangVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CSharpAuthor.Tests.RoslynBridgeTests;

/// <summary>
/// Compiles C# and hands back the symbols the compiler made of it.
/// </summary>
/// <remarks>
/// The bridge is tested against symbols Roslyn produced from real source, never against hand-built
/// fakes. A fake agrees with whatever the test author believed - that <c>int[,][]</c> nests the way
/// it reads, that a non-generic type inside a generic one is not generic - and those beliefs are
/// exactly what the conversion gets wrong.
/// </remarks>
internal static class TestCompilation
{
    /// <summary>
    /// Every assembly of the running framework, so <c>dynamic</c>, tuples and function pointers bind.
    /// </summary>
    private static readonly Lazy<IReadOnlyList<MetadataReference>> References = new(() =>
    {
        var platformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "";

        return platformAssemblies
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
    });

    /// <summary>
    /// Parses at the language version every tree here uses. A compilation cannot mix versions, so a
    /// generated file added to one has to be parsed the same way.
    /// </summary>
    public static SyntaxTree ParseTree(string source)
    {
        return CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(RoslynLangVersion.CSharp11));
    }

    public static CSharpCompilation Compile(string source)
    {
        var tree = ParseTree(source);

        return CSharpCompilation.Create(
            "BridgeTests",
            new[] { tree },
            References.Value,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    /// <summary>Compiles and fails the test if the source did not.</summary>
    public static CSharpCompilation CompileClean(string source)
    {
        var compilation = Compile(source);

        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors.Select(error => error.ToString())));

        return compilation;
    }

    /// <summary>
    /// The type of a field, given a declaration body. Fields are the shortest way to name a type in
    /// source and have the compiler bind it exactly as written.
    /// </summary>
    public static ITypeSymbol FieldType(string fieldDeclarations, string fieldName)
    {
        return FieldTypes(fieldDeclarations)[fieldName];
    }

    public static IReadOnlyDictionary<string, ITypeSymbol> FieldTypes(string fieldDeclarations)
    {
        var compilation = CompileClean(Wrap(fieldDeclarations));

        var holder = compilation.GetTypeByMetadataName("BridgeTestNamespace.Holder`1");

        Assert.NotNull(holder);

        var types = new Dictionary<string, ITypeSymbol>(StringComparer.Ordinal);

        foreach (var field in holder!.GetMembers().OfType<IFieldSymbol>())
        {
            types[field.Name] = field.Type;
        }

        return types;
    }

    /// <summary>The return type of a method declared in the shared holder.</summary>
    public static ITypeSymbol MethodReturnType(string members, string methodName)
    {
        var holder = NamedType(Wrap(members), "BridgeTestNamespace.Holder`1");

        foreach (var member in holder.GetMembers(methodName))
        {
            if (member is IMethodSymbol method)
            {
                return method.ReturnType;
            }
        }

        Assert.True(false, "no method named " + methodName);

        return null!;
    }

    /// <summary>The named type from a whole compilation unit.</summary>
    public static INamedTypeSymbol NamedType(string source, string metadataName)
    {
        var compilation = CompileClean(source);

        var symbol = compilation.GetTypeByMetadataName(metadataName);

        Assert.NotNull(symbol);

        return symbol!;
    }

    /// <summary>The type a <c>typeof(...)</c> names, including unbound generics.</summary>
    public static ITypeSymbol TypeOfArgument(string typeExpression)
    {
        var source = Wrap("public System.Type typeOfField = typeof(" + typeExpression + ");");

        var compilation = CompileClean(source);

        var tree = compilation.SyntaxTrees.First();

        var model = compilation.GetSemanticModel(tree);

        var typeOf = tree.GetRoot().DescendantNodes().OfType<TypeOfExpressionSyntax>().First();

        var type = model.GetTypeInfo(typeOf.Type).Type;

        Assert.NotNull(type);

        return type!;
    }

    /// <summary>The shared preamble: the types the conversion tests name.</summary>
    public static string Wrap(string fieldDeclarations)
    {
        return @"
#nullable enable
using System;
using System.Collections.Generic;

public class GlobalThing { public class Inner { } }

namespace BridgeTestNamespace {
    public class Outer<T> {
        public class Inner<U> { public class Deepest { } }
        public class PlainInner { }
    }
    public class Plain {
        public class Middle { public class Deepest { } }
    }
    public enum Color { Red, Green }
    [Flags] public enum Access { None = 0, Read = 1, Write = 2, All = 3 }
    public interface IThing { }
    public struct Val { }
    public class @event { public class @void { } }

    public unsafe class Holder<T> where T : class {
" + fieldDeclarations + @"
    }
}
";
    }

    /// <summary>
    /// The name the bridge writes, in one output mode.
    /// </summary>
    public static string Write(ITypeDefinition typeDefinition, TypeOutputMode mode = TypeOutputMode.ShortName)
    {
        var builder = new StringBuilder();

        typeDefinition.WriteTypeName(builder, mode);

        return builder.ToString();
    }
}
