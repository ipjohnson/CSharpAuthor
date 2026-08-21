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
        outputContext.Write($" {Name}");

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
            outputContext.Write(IndexName);
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
            if (writeInit)
            {
                outputContext.Write(" { get; init; }");
            }
            else if ((Set.Modifiers & ComponentModifier.Private) == ComponentModifier.Private)
            {
                outputContext.Write(" { get; private set; }");
            }
            else if ((Set.Modifiers & ComponentModifier.Protected) == ComponentModifier.Protected)
            {
                outputContext.Write(" { get; protected set; }");
            }
            else
            {
                outputContext.Write(" { get; set; }");
            }

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
            if ((Set.Modifiers & ComponentModifier.Private) == ComponentModifier.Private)
            {
                outputContext.Write("private ");
            }
            else if ((Set.Modifiers & ComponentModifier.Protected) == ComponentModifier.Protected)
            {
                outputContext.Write("protected ");
            }
            outputContext.Write("set");
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
        var virtualKeyword = GetVirtualModifier();

        outputContext.WriteIndent($"{modifier} ");

        if (!string.IsNullOrEmpty(virtualKeyword))
        {
            outputContext.Write(virtualKeyword);
            outputContext.WriteSpace();
        }
        else if ((Modifiers & ComponentModifier.Static) == ComponentModifier.Static)
        {
            outputContext.Write(KeyWords.Static);
            outputContext.WriteSpace();
        }
    }
}