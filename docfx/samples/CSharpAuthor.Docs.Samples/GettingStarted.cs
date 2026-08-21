using CSharpAuthor;
using static CSharpAuthor.SyntaxHelpers;

namespace CSharpAuthor.Docs.Samples;

/// <summary>Samples for docfx/docs/getting-started.md.</summary>
public static class GettingStarted
{
    /// <summary>The smallest thing that produces a file.</summary>
    public static string Smallest()
    {
        #region smallest
        var file = new CSharpFileDefinition("Acme.Greetings");

        var greeter = file.AddClass("Greeter");
        greeter.AddMethod("Greet");

        var output = new OutputContext();
        file.WriteOutput(output);

        string code = output.Output();
        #endregion

        return code;
    }

    /// <summary>A class with state, a constructor and a method with a body.</summary>
    public static string Greeter()
    {
        #region greeter
        var file = new CSharpFileDefinition("Acme.Greetings");

        var greeter = file.AddClass("Greeter");
        greeter.Modifiers |= ComponentModifier.Public;

        // Fields, constructors and methods are asked for, not spelled out.
        var name = greeter.AddField(TypeDefinition.Get(typeof(string)), "_name");
        name.Modifiers |= ComponentModifier.Private | ComponentModifier.Readonly;

        var constructor = greeter.AddConstructor();
        var nameParameter = constructor.AddParameter(typeof(string), "name");
        constructor.Assign(nameParameter).To(name.Instance);

        var greet = greeter.AddMethod("Greet");
        greet.Modifiers |= ComponentModifier.Public;
        greet.SetReturnType(typeof(string));
        greet.Return(Add(QuoteString("Hello, "), name.Instance));

        var output = new OutputContext();
        file.WriteOutput(output);

        string code = output.Output();
        #endregion

        return code;
    }
}
