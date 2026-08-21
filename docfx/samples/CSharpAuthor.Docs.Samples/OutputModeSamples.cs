using System.Collections.Generic;
using System.Threading.Tasks;
using CSharpAuthor;

namespace CSharpAuthor.Docs.Samples;

/// <summary>Samples for docfx/docs/output-modes.md.</summary>
public static class OutputModeSamples
{
    /// <summary>The same tree in all three modes.</summary>
    public static string ThreeModes()
    {
        #region three-modes
        static CSharpFileDefinition BuildFile()
        {
            var file = new CSharpFileDefinition("Acme.Reporting");

            var report = file.AddClass("Report");
            report.Modifiers |= ComponentModifier.Public;

            report.AddProperty(TypeDefinition.Get(typeof(List<string>)), "Lines")
                  .Modifiers |= ComponentModifier.Public;

            var task = TypeDefinition.Get(typeof(Task));

            var save = report.AddMethod("SaveAsync");
            save.Modifiers |= ComponentModifier.Public;
            save.SetReturnType(task);

            // Not the string "Task.CompletedTask" - see "Members reached off a type", below.
            save.Return(CodeOutputComponent.Get(task, "CompletedTask"));

            return file;
        }

        static string Render(TypeOutputMode mode)
        {
            var output = new OutputContext(new OutputContextOptions { TypeOutputMode = mode });
            BuildFile().WriteOutput(output);

            return output.Output();
        }

        string shortName = Render(TypeOutputMode.ShortName);
        string fullName = Render(TypeOutputMode.FullName);
        string global = Render(TypeOutputMode.Global);
        #endregion

        return "=== ShortName (default) ===\n" + shortName
             + "\n=== FullName ===\n" + fullName
             + "\n=== Global (recommended for generators) ===\n" + global;
    }

    /// <summary>Two types, one short name. ShortName mode aliases the loser instead of emitting CS0104.</summary>
    public static string CollisionAliasing()
    {
        #region collision-aliasing
        var file = new CSharpFileDefinition("Acme.Scheduling");

        var runner = file.AddClass("Runner");
        runner.Modifiers |= ComponentModifier.Public;

        // Two different types that both want to be spelled "Task".
        var frameworkTask = TypeDefinition.Get(typeof(Task));
        var ourTask = TypeDefinition.Get("Acme.Domain", "Task");

        runner.AddProperty(frameworkTask, "Running").Modifiers |= ComponentModifier.Public;
        runner.AddProperty(ourTask, "Pending").Modifiers |= ComponentModifier.Public;

        var output = new OutputContext(new OutputContextOptions
        {
            TypeOutputMode = TypeOutputMode.ShortName,
            AliasCollisions = true,     // the default
        });

        file.WriteOutput(output);
        string code = output.Output();
        #endregion

        return code;
    }

    /// <summary>
    /// A member reached off a type. Written as a string it tracks no namespace; handed the type it
    /// qualifies, aliases and derives a using like any other type reference.
    /// </summary>
    public static string MembersOffAType()
    {
        #region members-off-a-type
        var lifetime = TypeDefinition.Get("Microsoft.Extensions.DependencyInjection", "ServiceLifetime");

        static CSharpFileDefinition BuildFile(IOutputComponent value)
        {
            var file = new CSharpFileDefinition("Acme.Startup");

            var registration = file.AddClass("ServiceRegistration");
            registration.Modifiers |= ComponentModifier.Public;

            var describe = registration.AddMethod("Describe");
            describe.Modifiers |= ComponentModifier.Public | ComponentModifier.Static;
            describe.Assign(value).ToVar("lifetime");

            return file;
        }

        static string RenderGlobal(CSharpFileDefinition file)
        {
            var output = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });
            file.WriteOutput(output);

            return output.Output();
        }

        // A raw string. Nothing in the tree knows this text names a type, so nothing derives a
        // using for it and nothing qualifies it. In a file that qualifies everything else, this
        // line has nothing to resolve against.
        string asAString = RenderGlobal(BuildFile(CodeOutputComponent.Get("ServiceLifetime.Singleton")));

        // The same member, with the type left unrendered until serialization.
        string asAType = RenderGlobal(BuildFile(CodeOutputComponent.Get(lifetime, "Singleton")));
        #endregion

        return "=== written as a string: does not compile ===\n" + asAString
             + "\n=== written as a type plus a member ===\n" + asAType;
    }

    /// <summary>
    /// The one thing <c>global::</c> cannot name. Extension methods resolve through a using
    /// directive or not at all, so they need <c>AddUsingNamespace</c> even in Global mode.
    /// </summary>
    public static string ExtensionMethodsNeedAUsing()
    {
        #region extension-usings
        var file = new CSharpFileDefinition("Acme.Startup");

        var registration = file.AddClass("ServiceRegistration");
        registration.Modifiers |= ComponentModifier.Public;

        var register = registration.AddMethod("Register");
        register.Modifiers |= ComponentModifier.Public | ComponentModifier.Static;

        var services = register.AddParameter(
            TypeDefinition.Get("Microsoft.Extensions.DependencyInjection", "IServiceCollection"),
            "services");

        // AddSingleton is an extension method. C# resolves extension methods only through
        // using directives - there is no global::Namespace.Method form - so name the
        // namespace explicitly. Derived usings cannot do this for you: nothing in the tree
        // records which type the method hangs off.
        register.AddUsingNamespace("Microsoft.Extensions.DependencyInjection");
        register.AddIndentedStatement(
            services.Invoke("AddSingleton", SyntaxHelpers.TypeOf(TypeDefinition.Get("Acme.Services", "GreetingService"))));

        var output = new OutputContext(new OutputContextOptions
        {
            TypeOutputMode = TypeOutputMode.Global,
            EmitExplicitUsings = true,      // the default; false drops by-name usings in a qualifying mode
        });

        file.WriteOutput(output);
        string code = output.Output();
        #endregion

        return code;
    }

    /// <summary>A using naming the file's own namespace is noise. Declare it and it is dropped.</summary>
    public static string ContainingNamespace()
    {
        #region containing-namespace
        var file = new CSharpFileDefinition("Acme.Services");

        var factory = file.AddClass("GreetingServiceFactory");
        factory.Modifiers |= ComponentModifier.Public;

        // A type from the file's own namespace, and one from elsewhere.
        factory.AddProperty(TypeDefinition.Get("Acme.Services", "GreetingService"), "Service")
               .Modifiers |= ComponentModifier.Public;
        factory.AddProperty(TypeDefinition.Get("Acme.Diagnostics", "Log"), "Log")
               .Modifiers |= ComponentModifier.Public;

        var output = new OutputContext(new OutputContextOptions
        {
            TypeOutputMode = TypeOutputMode.ShortName,
            ContainingNamespace = "Acme.Services",
        });

        file.WriteOutput(output);
        string code = output.Output();
        #endregion

        return code;
    }
}
