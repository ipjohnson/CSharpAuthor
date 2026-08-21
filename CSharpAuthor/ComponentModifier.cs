using System;
using System.Text;

namespace CSharpAuthor;

[Flags]
public enum ComponentModifier
{
    None = 0,

    Public = 1,

    Protected = 2,

    Private = 4,

    Readonly = 8,

    Static = 16,

    Virtual = 32,

    Override = 64,

    Abstract = 128,

    Async = 256,

    Partial = 512,
    
    Internal = 1024,
    
    NoAccessibility = 2048,

    Sealed = 4096,

    /// <summary>
    /// <c>protected internal</c> - the union of the two, reachable from a derived type anywhere and
    /// from anything in this assembly.
    /// </summary>
    /// <remarks>
    /// C# spells two of its five accessibility levels with two keywords, and this enum is
    /// <see cref="FlagsAttribute"/>, so <c>Protected | Internal</c> is the only way to say it. It
    /// was accepted and then read one flag at a time, and the reader stopped at the first match:
    /// <c>internal</c>, with <c>protected</c> dropped. Named here so the combination is discoverable
    /// rather than something a caller has to know to construct.
    /// </remarks>
    ProtectedInternal = Protected | Internal,

    /// <summary>
    /// <c>private protected</c> - the intersection, reachable only from a derived type in this
    /// assembly. The most restrictive of the two-keyword levels.
    /// </summary>
    /// <remarks>
    /// This is the defect worth naming. Read one flag at a time, <c>Private | Protected</c> matched
    /// <c>protected</c> first and emitted that - which is <em>wider</em> than what was asked for,
    /// reachable from a derived type in any assembly. A member declared as the most restricted
    /// thing C# offers was published outside its own assembly, and nothing failed to compile.
    /// </remarks>
    PrivateProtected = Private | Protected,

}
/// <summary>
/// Reading a <see cref="ComponentModifier"/> as the C# keywords that spell it.
/// </summary>
public static class ComponentModifierExtensions
{
    /// <summary>
    /// Every modifier that is not an accessibility level, in the order C# convention puts them.
    /// </summary>
    /// <remarks>
    /// The order is <c>csharp_preferred_modifier_order</c> from this repository's .editorconfig,
    /// with <c>partial</c> last because it belongs immediately before the return type or the type
    /// keyword.
    /// </remarks>
    private static readonly (ComponentModifier Flag, string Keyword)[] OrderedModifiers =
    {
        (ComponentModifier.Static, "static"),
        (ComponentModifier.Virtual, "virtual"),
        (ComponentModifier.Abstract, "abstract"),
        (ComponentModifier.Sealed, "sealed"),
        (ComponentModifier.Override, "override"),
        (ComponentModifier.Readonly, "readonly"),
        (ComponentModifier.Async, "async"),
        (ComponentModifier.Partial, "partial"),
    };

    /// <summary>
    /// The non-accessibility modifier keywords in <paramref name="modifiers"/> that
    /// <paramref name="applicable"/> allows, in canonical order, each followed by a space.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every caller of this used to be a chain of <c>else if</c>, which meant exactly one modifier
    /// could ever be written and the rest were dropped in silence. <c>sealed override</c> - the
    /// only form in which <c>sealed</c> is legal on a member - lost its <c>sealed</c>;
    /// <c>static abstract</c> lost its <c>abstract</c>; <c>partial</c> and <c>readonly</c> were
    /// never written at all, so a partial method came out as a duplicate definition and a readonly
    /// struct came out mutable.
    /// </para>
    /// <para>
    /// Combinations are not validated. <c>abstract sealed</c> on a method is CS0238, and this
    /// writes it, the same way the rest of the library writes what it is handed and lets the
    /// compiler be the one to object. A dropped modifier is silent; a rejected one is not.
    /// </para>
    /// </remarks>
    public static string GetModifierKeywords(
        this ComponentModifier modifiers, ComponentModifier applicable)
    {
        var builder = new StringBuilder();

        foreach (var ordered in OrderedModifiers)
        {
            if ((applicable & ordered.Flag) == ordered.Flag &&
                (modifiers & ordered.Flag) == ordered.Flag)
            {
                builder.Append(ordered.Keyword);
                builder.Append(' ');
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// What a type declaration may be marked with.
    /// </summary>
    public const ComponentModifier TypeModifiers =
        ComponentModifier.Static | ComponentModifier.Abstract | ComponentModifier.Sealed |
        ComponentModifier.Readonly | ComponentModifier.Partial;

    /// <summary>
    /// What a method declaration may be marked with.
    /// </summary>
    public const ComponentModifier MethodModifiers =
        ComponentModifier.Static | ComponentModifier.Virtual | ComponentModifier.Abstract |
        ComponentModifier.Sealed | ComponentModifier.Override | ComponentModifier.Readonly |
        ComponentModifier.Async | ComponentModifier.Partial;

    /// <summary>
    /// What a property or event declaration may be marked with.
    /// </summary>
    public const ComponentModifier PropertyModifiers =
        ComponentModifier.Static | ComponentModifier.Virtual | ComponentModifier.Abstract |
        ComponentModifier.Sealed | ComponentModifier.Override | ComponentModifier.Readonly;

    /// <summary>
    /// The accessibility keywords <paramref name="modifiers"/> declares, or
    /// <paramref name="defaultString"/> when it declares none.
    /// </summary>
    /// <remarks>
    /// The two-keyword levels are tested first, and they have to be:
    /// <see cref="ComponentModifier"/> is a flags enum, so <c>private protected</c> is
    /// <c>Private | Protected</c> and matches both single-flag tests below. Reading one flag at a
    /// time returned <c>protected</c> for it - a <em>wider</em> accessibility than was declared -
    /// and <c>internal</c> for <c>protected internal</c>. Both compiled, which is why neither was
    /// noticed.
    /// </remarks>
    public static string GetAccessibilityKeywords(
        this ComponentModifier modifiers, string defaultString = "")
    {
        if ((modifiers & ComponentModifier.NoAccessibility) == ComponentModifier.NoAccessibility)
        {
            return "";
        }

        if ((modifiers & ComponentModifier.ProtectedInternal) == ComponentModifier.ProtectedInternal)
        {
            return KeyWords.Protected + " " + KeyWords.Internal;
        }

        if ((modifiers & ComponentModifier.PrivateProtected) == ComponentModifier.PrivateProtected)
        {
            return KeyWords.Private + " " + KeyWords.Protected;
        }

        if ((modifiers & ComponentModifier.Internal) == ComponentModifier.Internal)
        {
            return KeyWords.Internal;
        }

        if ((modifiers & ComponentModifier.Public) == ComponentModifier.Public)
        {
            return KeyWords.Public;
        }

        if ((modifiers & ComponentModifier.Protected) == ComponentModifier.Protected)
        {
            return KeyWords.Protected;
        }

        if ((modifiers & ComponentModifier.Private) == ComponentModifier.Private)
        {
            return KeyWords.Private;
        }

        return defaultString;
    }

    /// <summary>
    /// The accessibility keywords for a property accessor, which are written only when they narrow
    /// the property's own.
    /// </summary>
    /// <remarks>
    /// <c>public</c> is deliberately absent from the result: an accessor may only be more
    /// restrictive than its property, so <c>public set</c> on a public property is CS0273 rather
    /// than a redundancy. A caller marking the accessor the same as the property means "no
    /// narrowing", and that is written as nothing at all.
    /// </remarks>
    public static string GetAccessorAccessibilityKeywords(this ComponentModifier modifiers)
    {
        var keywords = modifiers.GetAccessibilityKeywords();

        return keywords == KeyWords.Public ? "" : keywords;
    }
}
