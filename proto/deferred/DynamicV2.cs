#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpAuthor;
using Deferred;

namespace DynamicTest;

// ---------------------------------------------------------------------------
// SAME payload, ONE new requirement:
//
//   "Wrap every conditional registration in a try/catch that logs, and hoist a
//    static logger field the catch block uses."
//
// This is deliberately the shape of change that arrives late in a real
// generator: it deepens nesting for SOME items, and it needs a new class-level
// member discovered from inside a loop body.
// ---------------------------------------------------------------------------
public static class DynamicV2
{
    public static string WithTree(List<Svc> model) =>
        WithTreeStyled(model, new StyleOptions { ContainingNamespace = "Acme.Generated" });

    public static string WithTreeStyled(List<Svc> model, StyleOptions style)
    {
        var file = new CSharpFileDefinition("Acme.Generated") { FileScopedNamespace = style.FileScopedNamespace };
        var cls = file.AddClass("Registrations");
        cls.Modifiers |= ComponentModifier.Public | ComponentModifier.Static;

        var services = TypeDefinition.Get(
            TypeDefinitionEnum.InterfaceDefinition,
            "Microsoft.Extensions.DependencyInjection", "IServiceCollection");
        var env = TypeDefinition.Get(
            TypeDefinitionEnum.InterfaceDefinition, "Acme.Runtime", "IModuleEnvironment");
        var logger = TypeDefinition.Get(
            TypeDefinitionEnum.InterfaceDefinition, "Acme.Runtime", "ILogger");

        var method = cls.AddMethod("Register");
        method.Modifiers |= ComponentModifier.Public | ComponentModifier.Static;
        method.SetReturnType(services);
        var svcParam = method.AddParameter(services, "services");

        var envParam = model.Any(s => s.Conditional)
            ? method.AddParameter(env, "environment")
            : null;

        var emittedFactories = new HashSet<string>();
        FieldDefinition? loggerField = null;                          // + NEW

        foreach (var svc in model)
        {
            var type = TypeDefinition.Get(svc.Ns, svc.Name);

            var block = svc.Conditional && envParam != null
                ? method.If($"{envParam.Name}.Is(\"prod\")")
                : (BaseBlockDefinition)method;

            if (svc.Conditional && envParam != null)                  // + NEW
            {                                                         // + NEW
                loggerField ??= AddLogger(cls, logger);               // + NEW
                var t = block.Try();                                  // + NEW
                t.Catch(typeof(Exception), "e")                       // + NEW
                 .AddCode($"{loggerField.Name}.Error(e);");           // + NEW
                block = t;                                            // + NEW
            }                                                         // + NEW

            if (svc.NeedsFactory)
            {
                var factoryName = "Create" + svc.Name;

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

        var ctx = new DeferredOutputContext(style);
        file.WriteOutput(ctx);
        return ctx.Output();
    }

    static FieldDefinition AddLogger(ClassDefinition cls, ITypeDefinition logger)   // + NEW
    {
        var f = cls.AddField(logger, "_logger");
        f.Modifiers |= ComponentModifier.Private | ComponentModifier.Static;
        return f;
    }

    // =======================================================================
    public static string WithStringBuilder(List<Svc> model)
    {
        var fieldsBuf = new StringBuilder();          // + NEW third buffer
        var factoriesBuf = new StringBuilder();
        var bodyBuf = new StringBuilder();

        var usings = new SortedSet<string>();
        var emittedFactories = new HashSet<string>();
        var loggerEmitted = false;                    // + NEW

        var anyConditional = model.Any(s => s.Conditional);
        usings.Add("Microsoft.Extensions.DependencyInjection");
        if (anyConditional) usings.Add("Acme.Runtime");

        string Ind(int d) => new string(' ', d * 4);

        foreach (var svc in model)
        {
            usings.Add(svc.Ns);

            var depth = 2;
            if (svc.Conditional && anyConditional)
            {
                bodyBuf.Append(Ind(depth)).Append("if (environment.Is(\"prod\"))\n");
                bodyBuf.Append(Ind(depth)).Append("{\n");
                depth++;

                if (!loggerEmitted)                                     // + NEW
                {                                                      // + NEW
                    fieldsBuf.Append(Ind(1))                           // + NEW
                        .Append("private static ILogger _logger;\n\n"); // + NEW
                    loggerEmitted = true;                              // + NEW
                }                                                      // + NEW

                bodyBuf.Append(Ind(depth)).Append("try\n");            // + NEW
                bodyBuf.Append(Ind(depth)).Append("{\n");              // + NEW
                depth++;                                               // + NEW
            }

            if (svc.NeedsFactory)
            {
                var factoryName = "Create" + svc.Name;
                if (emittedFactories.Add(factoryName))
                {
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
                depth--;                                                       // + NEW
                bodyBuf.Append(Ind(depth)).Append("}\n");                      // + NEW
                bodyBuf.Append(Ind(depth)).Append("catch (Exception e)\n");    // + NEW
                bodyBuf.Append(Ind(depth)).Append("{\n");                      // + NEW
                bodyBuf.Append(Ind(depth + 1)).Append("_logger.Error(e);\n");  // + NEW
                bodyBuf.Append(Ind(depth)).Append("}\n");                      // + NEW
                usings.Add("System");                                          // + NEW
                depth--;
                bodyBuf.Append(Ind(depth)).Append("}\n");
            }
        }

        bodyBuf.Append(Ind(2)).Append("return services;\n");

        var sb = new StringBuilder();
        foreach (var u in usings) sb.Append("using ").Append(u).Append(";\n");
        sb.Append('\n').Append("namespace Acme.Generated;\n\n");
        sb.Append("public static class Registrations\n{\n");
        sb.Append(fieldsBuf);                                    // + NEW
        sb.Append(factoriesBuf);
        sb.Append(Ind(1)).Append("public static IServiceCollection Register(IServiceCollection services");
        if (anyConditional) sb.Append(", IModuleEnvironment environment");
        sb.Append(")\n").Append(Ind(1)).Append("{\n");
        sb.Append(bodyBuf);
        sb.Append(Ind(1)).Append("}\n}\n");
        return sb.ToString();
    }
}
