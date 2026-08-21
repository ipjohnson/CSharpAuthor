using System;
using System.Globalization;
using System.Threading;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// Runs a component through the library and hands back what it wrote.
/// </summary>
internal static class Emit
{
    /// <summary>
    /// One component's output, with nothing around it.
    /// </summary>
    public static string Component(IOutputComponent component, OutputContextOptions? options = null)
    {
        var context = new OutputContext(options);

        component.WriteOutput(context);

        return context.Output();
    }

    /// <summary>
    /// A whole file, including the using directives the context derived.
    /// </summary>
    public static string File(CSharpFileDefinition file, OutputContextOptions? options = null) =>
        Component(file, options);

    /// <summary>
    /// A type reference as the library renders it, which is the smallest unit an adversary case can
    /// be about.
    /// </summary>
    public static string TypeName(
        ITypeDefinition type, TypeOutputMode mode = TypeOutputMode.ShortName)
    {
        var builder = new System.Text.StringBuilder();

        type.WriteTypeName(builder, mode);

        return builder.ToString();
    }

    /// <summary>
    /// Runs <paramref name="action"/> with the thread's culture set to <paramref name="cultureName"/>.
    /// </summary>
    /// <remarks>
    /// The culture defect is the one that never reproduces for the person who wrote the generator and
    /// always reproduces for someone else, so the tests for it have to install the culture rather
    /// than hope for it. The culture is restored afterwards even when the assertion fails, because
    /// xUnit runs a collection on one thread and a leaked culture would then contaminate every test
    /// after it - turning one honest failure into a dozen confusing ones.
    /// </remarks>
    public static T InCulture<T>(string cultureName, Func<T> action)
    {
        var culture = Thread.CurrentThread.CurrentCulture;
        var uiCulture = Thread.CurrentThread.CurrentUICulture;

        try
        {
            var target = new CultureInfo(cultureName);

            Thread.CurrentThread.CurrentCulture = target;
            Thread.CurrentThread.CurrentUICulture = target;

            return action();
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = uiCulture;
        }
    }
}
