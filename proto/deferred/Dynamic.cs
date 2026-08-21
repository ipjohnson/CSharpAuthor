#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpAuthor;
using Deferred;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DynamicTest;

// ---------------------------------------------------------------------------
// A payload with the properties that make real generators hard. Every one of
// these appears in DependencyFileWriter.
//
//  1. BACK-PATCHING   a field/method is discovered while emitting a method BODY
//                     and must land in the class, above where we currently are
//  2. LOOK-AHEAD      a parameter exists only if SOME later item needs it
//  3. DEDUP           the same factory must not be emitted twice
//  4. NESTING         depth varies per item at runtime
//  5. USINGS          collected from types used anywhere in the file
// ---------------------------------------------------------------------------
public sealed record Svc(
    string Name,
    string Ns,
    bool Conditional,
    bool NeedsFactory,
    string Lifetime);

public static class Dynamic
{
    public static List<Svc> Model(int n)
    {
        var list = new List<Svc>(n);
        for (var i = 0; i < n; i++)
            list.Add(new Svc(
                Name: "Svc" + i,
                Ns: i % 3 == 0 ? "Acme.Core" : "Acme.Ext",
                Conditional: i % 4 == 0,
                NeedsFactory: i % 5 == 0,
                // two services deliberately share a factory shape, to force dedup
                Lifetime: i % 2 == 0 ? "Singleton" : "Scoped"));
        return list;
    }

    // =======================================================================
    // TREE VERSION
    // =======================================================================
    public static string WithTree(List<Svc> model)
    {
        var file = new CSharpFileDefinition("Acme.Generated") { FileScopedNamespace = true };
        var cls = file.AddClass("Registrations");
        cls.Modifiers |= ComponentModifier.Public | ComponentModifier.Static;

        var services = TypeDefinition.Get(
            TypeDefinitionEnum.InterfaceDefinition,
            "Microsoft.Extensions.DependencyInjection", "IServiceCollection");
        var env = TypeDefinition.Get(
            TypeDefinitionEnum.InterfaceDefinition, "Acme.Runtime", "IModuleEnvironment");

        var method = cls.AddMethod("Register");
        method.Modifiers |= ComponentModifier.Public | ComponentModifier.Static;
        method.SetReturnType(services);
        var svcParam = method.AddParameter(services, "services");

        // (2) LOOK-AHEAD: one LINQ query, before the body is written.
        var envParam = model.Any(s => s.Conditional)
            ? method.AddParameter(env, "environment")
            : null;

        var emittedFactories = new HashSet<string>();

        foreach (var svc in model)
        {
            var type = TypeDefinition.Get(svc.Ns, svc.Name);

            // (4) NESTING: the block is either the method or a nested if.
            var block = svc.Conditional && envParam != null
                ? method.If($"{envParam.Name}.Is(\"prod\")")
                : (BaseBlockDefinition)method;

            if (svc.NeedsFactory)
            {
                var factoryName = "Create" + svc.Name;

                // (3) DEDUP + (1) BACK-PATCH: add a method to the CLASS while
                // we are positioned inside another method's body.
                if (emittedFactories.Add(factoryName))
                {
                    var f = cls.AddMethod(factoryName);
                    f.Modifiers |= ComponentModifier.Private | ComponentModifier.Static;
                    f.SetReturnType(type);
                    f.AddParameter(TypeDefinition.Get(typeof(IServiceProvider)), "provider");
                    f.Return(new NewStatement(type, Array.Empty<object>()));
                }

                block.AddCode($"services.Add{svc.Lifetime}(typeof({{arg1}}), {factoryName});", type);
            }
            else
            {
                block.AddCode($"services.Add{svc.Lifetime}(typeof({{arg1}}));", type);
            }
        }

        method.Return(svcParam.Name);

        var ctx = new DeferredOutputContext(new StyleOptions { ContainingNamespace = "Acme.Generated" });
        file.WriteOutput(ctx);
        return ctx.Output();       // (5) usings derived from what was written
    }

    // =======================================================================
    // STRINGBUILDER VERSION — written competently, not as a strawman.
    // Section buffers, an indent helper, a using set.
    // =======================================================================
    public static string WithStringBuilder(List<Svc> model)
    {
        // (1) BACK-PATCHING forces separate buffers per class section, because
        // the factory methods are discovered while writing Register's body but
        // must appear as siblings of it.
        var factoriesBuf = new StringBuilder();
        var bodyBuf = new StringBuilder();

        // (5) USINGS must be collected manually — nothing derives them.
        var usings = new SortedSet<string>();

        var emittedFactories = new HashSet<string>();

        // (2) LOOK-AHEAD: same query, but the RESULT must be threaded into the
        // signature, which is written after the body — so signature emission
        // has to be deferred too.
        var anyConditional = model.Any(s => s.Conditional);
        usings.Add("Microsoft.Extensions.DependencyInjection");
        if (anyConditional) usings.Add("Acme.Runtime");

        // indentation is now a parameter every helper must carry
        string Ind(int d) => new string(' ', d * 4);

        foreach (var svc in model)
        {
            usings.Add(svc.Ns);

            // (4) NESTING: depth is a local variable the author must maintain.
            var depth = 2;
            if (svc.Conditional && anyConditional)
            {
                bodyBuf.Append(Ind(depth)).Append("if (environment.Is(\"prod\"))\n");
                bodyBuf.Append(Ind(depth)).Append("{\n");
                depth++;
            }

            if (svc.NeedsFactory)
            {
                var factoryName = "Create" + svc.Name;
                if (emittedFactories.Add(factoryName))
                {
                    // (1) BACK-PATCH: append to the OTHER buffer, at a different
                    // indent level, from inside this loop.
                    factoriesBuf.Append(Ind(1))
                        .Append("private static ").Append(svc.Name)
                        .Append(' ').Append(factoryName).Append("(IServiceProvider provider)\n");
                    factoriesBuf.Append(Ind(1)).Append("{\n");
                    factoriesBuf.Append(Ind(2)).Append("return new ").Append(svc.Name).Append("();\n");
                    factoriesBuf.Append(Ind(1)).Append("}\n\n");
                    usings.Add("System");
                }

                bodyBuf.Append(Ind(depth))
                    .Append("services.Add").Append(svc.Lifetime)
                    .Append("(typeof(").Append(svc.Name).Append("), ").Append(factoryName).Append(");\n");
            }
            else
            {
                bodyBuf.Append(Ind(depth))
                    .Append("services.Add").Append(svc.Lifetime)
                    .Append("(typeof(").Append(svc.Name).Append("));\n");
            }

            if (svc.Conditional && anyConditional)
            {
                depth--;
                bodyBuf.Append(Ind(depth)).Append("}\n");
            }
        }

        bodyBuf.Append(Ind(2)).Append("return services;\n");

        // assemble — only now can the signature be written
        var sb = new StringBuilder();
        foreach (var u in usings) sb.Append("using ").Append(u).Append(";\n");
        sb.Append('\n').Append("namespace Acme.Generated;\n\n");
        sb.Append("public static class Registrations\n{\n");
        sb.Append(factoriesBuf);
        sb.Append(Ind(1)).Append("public static IServiceCollection Register(IServiceCollection services");
        if (anyConditional) sb.Append(", IModuleEnvironment environment");
        sb.Append(")\n").Append(Ind(1)).Append("{\n");
        sb.Append(bodyBuf);
        sb.Append(Ind(1)).Append("}\n}\n");
        return sb.ToString();
    }

    // =======================================================================
    public static void Run()
    {
        var model = Model(12);

        var tree = WithTree(model);
        var sbOut = WithStringBuilder(model);

        Console.WriteLine("################ TREE OUTPUT ################");
        Console.WriteLine(tree);
        Check("tree", tree);

        Console.WriteLine("################ STRINGBUILDER OUTPUT ################");
        Console.WriteLine(sbOut);
        Check("stringbuilder", sbOut);

        RunV2();
        Console.WriteLine("\n################ THE ACTUAL TEST ################");
        Console.WriteLine("Now change ONE requirement and see what each version costs.");
        RequirementChange();
    }

    public static void RunV2()
    {
        var model = Model(12);
        Console.WriteLine("################ V2 TREE (with try/catch + logger) ################");
        var t = DynamicV2.WithTree(model);
        Console.WriteLine(t);
        Check("v2 tree", t);
        var s2 = DynamicV2.WithStringBuilder(model);
        Console.WriteLine("################ V2 STRINGBUILDER ################");
        Console.WriteLine(s2);
        Check("v2 stringbuilder", s2);
        Console.WriteLine($"\n  outputs identical: {NormalizeWs(t) == NormalizeWs(s2)}");
    }

    static string NormalizeWs(string s) =>
        string.Join("\n", s.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));

    static void RequirementChange()
    {
        Console.WriteLine(@"
  Requirement: ""wrap every conditional registration in a try/catch, and
  hoist a static logger field the catch block uses.""

  TREE       : the catch is a nested block object; the logger field is
               cls.AddField(...) called from wherever you notice you need it.
               Indentation of everything inside shifts automatically.
               -> 2 call sites, no existing line edited.

  BUILDER    : every line inside the conditional needs depth+1, so the depth
               variable's arithmetic changes; the logger needs a THIRD buffer
               (fields must precede methods); and the writer that discovers it
               is three call levels deep inside the body loop, so the buffer
               has to be threaded down or made a field.
               -> every emit site touched, plus new plumbing.");
    }

    static void Check(string label, string source)
    {
        var opts = new CSharpParseOptions(LanguageVersion.Preview);
        var tree = CSharpSyntaxTree.ParseText(source, opts);
        var stub = CSharpSyntaxTree.ParseText(@"
namespace Microsoft.Extensions.DependencyInjection {
  public interface IServiceCollection { }
  public static class E {
    public static IServiceCollection AddSingleton(this IServiceCollection s, System.Type t) => s;
    public static IServiceCollection AddScoped(this IServiceCollection s, System.Type t) => s;
    public static IServiceCollection AddSingleton(this IServiceCollection s, System.Type t, System.Func<System.IServiceProvider, object> f) => s;
    public static IServiceCollection AddScoped(this IServiceCollection s, System.Type t, System.Func<System.IServiceProvider, object> f) => s;
  }
}
namespace Acme.Runtime { public interface IModuleEnvironment { bool Is(string n); } public interface ILogger { void Error(System.Exception e); } }
namespace Acme.Core { " + string.Join(" ", Enumerable.Range(0, 12).Where(i => i % 3 == 0).Select(i => $"public class Svc{i} {{ }}")) + @" }
namespace Acme.Ext { " + string.Join(" ", Enumerable.Range(0, 12).Where(i => i % 3 != 0).Select(i => $"public class Svc{i} {{ }}")) + @" }", opts);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location));
        var comp = CSharpCompilation.Create("d", new[] { tree, stub }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var errs = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Take(3).ToList();
        Console.WriteLine(errs.Count == 0 ? $"  >>> {label}: COMPILES" : $"  >>> {label}: ERRORS");
        foreach (var e in errs) Console.WriteLine("      " + e.Id + ": " + e.GetMessage());
    }
}
