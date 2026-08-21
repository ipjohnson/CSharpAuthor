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

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        WriteAccessModifiers(outputContext);

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
            var setterKeyword = Set.IsInit ? "init" : "set";
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