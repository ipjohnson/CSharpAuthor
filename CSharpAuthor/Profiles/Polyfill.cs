using System;
using System.Collections.Generic;

namespace CSharpAuthor.Profiles;

/// <summary>
/// A support type the compiler needs in order to accept a feature, which an older target
/// framework may not ship.
/// </summary>
#if CSHARPAUTHOR_PUBLIC_API
public
#endif
enum PolyfillType
{
    /// <summary>Required by <c>init</c> accessors.</summary>
    IsExternalInit,

    /// <summary>Required by <c>required</c> members.</summary>
    RequiredMemberAttribute,

    /// <summary>Also required by <c>required</c> members; the compiler applies it itself.</summary>
    CompilerFeatureRequiredAttribute,

    /// <summary>Lets a constructor declare that it sets every <c>required</c> member.</summary>
    SetsRequiredMembersAttribute
}

/// <summary>
/// The support types, written in syntax old enough that emitting them can never itself need a
/// newer language version than the code they are supporting.
/// </summary>
/// <remarks>
/// They are <c>internal</c> so two assemblies that both emit them do not collide, and written as
/// block namespaces so they can sit beside anything - except a file-scoped namespace, which C#
/// forbids sharing a file with any other namespace declaration. <see cref="ProfileEmitter"/>
/// falls back to a block namespace for the whole file when a polyfill has to be emitted into it.
/// </remarks>
#if CSHARPAUTHOR_PUBLIC_API
public
#endif
static class Polyfill
{
    /// <summary>The feature a polyfill supports.</summary>
    public static IEnumerable<PolyfillType> For(LanguageFeature feature)
    {
        switch (feature)
        {
            case LanguageFeature.InitOnlyProperties:
                yield return PolyfillType.IsExternalInit;
                break;
            case LanguageFeature.RequiredMembers:
                // The compiler applies CompilerFeatureRequired itself, and refuses `required`
                // without it. Emitting only RequiredMemberAttribute produces CS0656.
                yield return PolyfillType.RequiredMemberAttribute;
                yield return PolyfillType.CompilerFeatureRequiredAttribute;
                break;
        }
    }

    /// <summary>The namespace the type has to be declared in.</summary>
    public static string NamespaceOf(PolyfillType type) =>
        type == PolyfillType.SetsRequiredMembersAttribute
            ? "System.Diagnostics.CodeAnalysis"
            : "System.Runtime.CompilerServices";

    /// <summary>The type's name.</summary>
    public static string NameOf(PolyfillType type) => type.ToString();

    /// <summary>
    /// Writes the type, wrapped in a block namespace.
    /// </summary>
    public static void Write(IOutputContext outputContext, PolyfillType type)
    {
        if (outputContext == null)
        {
            throw new ArgumentNullException(nameof(outputContext));
        }

        outputContext.WriteIndentedLine("namespace " + NamespaceOf(type));
        outputContext.OpenScope();

        switch (type)
        {
            case PolyfillType.IsExternalInit:
                outputContext.WriteIndentedLine("internal static class IsExternalInit");
                outputContext.OpenScope();
                outputContext.CloseScope();
                break;

            case PolyfillType.RequiredMemberAttribute:
                WriteAttributeUsage(
                    outputContext,
                    "global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct | " +
                    "global::System.AttributeTargets.Field | global::System.AttributeTargets.Property",
                    allowMultiple: false);
                outputContext.WriteIndentedLine(
                    "internal sealed class RequiredMemberAttribute : global::System.Attribute");
                outputContext.OpenScope();
                outputContext.CloseScope();
                break;

            case PolyfillType.CompilerFeatureRequiredAttribute:
                WriteAttributeUsage(outputContext, "global::System.AttributeTargets.All", allowMultiple: true);
                outputContext.WriteIndentedLine(
                    "internal sealed class CompilerFeatureRequiredAttribute : global::System.Attribute");
                outputContext.OpenScope();
                outputContext.WriteIndentedLine("private readonly string _featureName;");
                outputContext.WriteIndentedLine("private bool _isOptional;");
                outputContext.WriteIndentedLine("public CompilerFeatureRequiredAttribute(string featureName)");
                outputContext.OpenScope();
                outputContext.WriteIndentedLine("_featureName = featureName;");
                outputContext.CloseScope();
                outputContext.WriteIndentedLine("public string FeatureName { get { return _featureName; } }");
                outputContext.WriteIndentedLine(
                    "public bool IsOptional { get { return _isOptional; } set { _isOptional = value; } }");
                outputContext.WriteIndentedLine("public const string RefStructs = \"RefStructs\";");
                outputContext.WriteIndentedLine("public const string RequiredMembers = \"RequiredMembers\";");
                outputContext.CloseScope();
                break;

            case PolyfillType.SetsRequiredMembersAttribute:
                WriteAttributeUsage(
                    outputContext, "global::System.AttributeTargets.Constructor", allowMultiple: false);
                outputContext.WriteIndentedLine(
                    "internal sealed class SetsRequiredMembersAttribute : global::System.Attribute");
                outputContext.OpenScope();
                outputContext.CloseScope();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown polyfill.");
        }

        outputContext.CloseScope();
    }

    private static void WriteAttributeUsage(IOutputContext outputContext, string targets, bool allowMultiple)
    {
        outputContext.WriteIndentedLine(
            "[global::System.AttributeUsage(" + targets +
            ", AllowMultiple = " + (allowMultiple ? "true" : "false") + ", Inherited = false)]");
    }
}
