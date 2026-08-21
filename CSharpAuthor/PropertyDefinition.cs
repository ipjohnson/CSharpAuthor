using System.Collections.Generic;

namespace CSharpAuthor;

public class PropertyDefinition : BaseOutputComponent, INamedComponent
{
    public PropertyDefinition(ITypeDefinition type, string name)
    {
        Name = name;
        Type = type;

        Get = new PropertyMethodDefinition();
        Set = new PropertyMethodDefinition();
    }

    public string Name { get; }
        
    public ITypeDefinition Type { get; }

    public PropertyMethodDefinition Get { get; }

    public PropertyMethodDefinition? Set { get; set; }

    /// <summary>
    /// The type of a single index, for the common <c>this[int index]</c> shape. Ignored when
    /// <see cref="IndexParameters"/> has entries.
    /// </summary>
    public ITypeDefinition? IndexType { get; set; }

    /// <summary>
    /// The name of the single index declared through <see cref="IndexType"/>.
    /// </summary>
    public string IndexName { get; set; } = "index";

    /// <summary>
    /// Indices for an indexer that takes more than one, such as <c>this[int row, int column]</c>.
    /// Takes precedence over <see cref="IndexType"/>.
    /// </summary>
    public List<ParameterDefinition> IndexParameters { get; } = new();

    public PropertyDefinition AddIndexParameter(ITypeDefinition type, string name)
    {
        IndexParameters.Add(new ParameterDefinition(type, name));

        return this;
    }

    private bool IsIndexer => IndexParameters.Count > 0 || IndexType != null;

    public InstanceDefinition Instance => new(Name);

    public IOutputComponent? DefaultValue { get; set; }

    /// <summary>
    /// Whether the property is <c>required</c>: the caller has to set it, and the compiler checks.
    /// </summary>
    /// <remarks>
    /// C# 11, and polyfillable - see <see cref="LanguageFeature.RequiredMembers"/>. Below it the
    /// keyword is dropped and a <c>// DOWNLEVEL:</c> comment says that nothing is enforcing the
    /// initialisation any more, because a property that silently stops being required is exactly
    /// the kind of change nobody notices until something is null in production.
    /// </remarks>
    public bool IsRequired { get; set; }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        var session = outputContext.EmitSession();

        // Asked before anything for this member is written: a `// DOWNLEVEL:` comment is a line
        // of its own and cannot be inserted into a half-written one. With no profile in force the
        // session answers yes to both, which is what V1 did.
        var writeInit = Set is { IsInit: true } &&
                        session.MayEmit(LanguageFeature.InitOnlyProperties, outputContext, Name);

        var writeRequired = IsRequired &&
                            session.MayEmit(LanguageFeature.RequiredMembers, outputContext, Name);

        WriteAccessModifiers(outputContext);

        if (writeRequired)
        {
            outputContext.Write("required ");
        }

        outputContext.Write(Type);
        // An indexer is declared as `this[...]`, where `this` is the keyword and not a name, so it
        // is the one property whose name must not be escaped.
        outputContext.Write(
            " " + (IsIndexer ? Name : CSharpIdentifier.Escape(Name)));

        if (IndexParameters.Count > 0)
        {
            outputContext.Write("[");

            for (var i = 0; i < IndexParameters.Count; i++)
            {
                if (i > 0)
                {
                    outputContext.Write(", ");
                }

                IndexParameters[i].WriteWithSignature(outputContext);
            }

            outputContext.Write("]");
        }
        else if (IndexType != null)
        {
            outputContext.Write("[");
            outputContext.Write(IndexType);
            outputContext.Write(" ");
            outputContext.Write(CSharpIdentifier.Escape(IndexName));
            outputContext.Write("]");
        }

        // An indexer has no auto-property or expression-bodied form to fall back to, so it always
        // writes its accessors out in full.
        if (Set == null && !IsIndexer)
        {
            if (Get.StatementCount == 0)
            {
                outputContext.WriteLine(" { get; }");
                return;
            }
                
            if (Get.LambdaSyntax)
            {
                Get.WriteOutput(outputContext);
                return;
            }
        }
        else if (!IsIndexer &&
                 Get.StatementCount == 0 &&
                 Set is { StatementCount: 0 })
        {
            // writeInit, not Set.IsInit: below C#9 `init` downlevels to `set`.
            var setterKeyword = writeInit ? "init" : "set";
            var setterAccess = Set.Modifiers.GetAccessorAccessibilityKeywords();

            if (!string.IsNullOrEmpty(setterAccess))
            {
                setterAccess += " ";
            }

            outputContext.Write(" { get; " + setterAccess + setterKeyword + "; }");

            if (DefaultValue != null)
            {
                outputContext.Write(" = ");
                DefaultValue.WriteOutput(outputContext);
                outputContext.Write(";");
            }
            
            outputContext.WriteLine();
            
            return;
        }

        outputContext.WriteLine();
        outputContext.OpenScope();

        outputContext.WriteIndent("get");
        Get.WriteOutput(outputContext);

        if (Set != null)
        {
            outputContext.WriteIndent();

            var setterAccess = Set.Modifiers.GetAccessorAccessibilityKeywords();

            if (!string.IsNullOrEmpty(setterAccess))
            {
                outputContext.Write(setterAccess);
                outputContext.WriteSpace();
            }

            outputContext.Write(Set.IsInit ? "init" : "set");
            Set.WriteOutput(outputContext);
        }

        outputContext.CloseScope();
    }

    protected override void WriteComment(IOutputContext outputContext)
    {
        if (string.IsNullOrWhiteSpace(Comment))
        {
            return;
        }
        
        DocumentationComment.WriteSummary(outputContext.WriteIndentedLine, Comment);
    }

    protected virtual void WriteAccessModifiers(IOutputContext outputContext)
    {
        var modifier = GetAccessModifier("public");

        outputContext.WriteIndent($"{modifier} ");

        outputContext.Write(
            Modifiers.GetModifierKeywords(ComponentModifierExtensions.PropertyModifiers));
    }
}