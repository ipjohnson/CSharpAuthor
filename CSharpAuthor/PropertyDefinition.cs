namespace CSharpAuthor;

public class PropertyDefinition : BaseOutputComponent
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

    public ITypeDefinition? IndexType { get; set; }

    public string IndexName { get; set; } = "index";

    public InstanceDefinition Instance => new(Name);
    
    public IOutputComponent? DefaultValue { get; set; }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        WriteAccessModifiers(outputContext);

        outputContext.Write(Type);
        outputContext.Write($" {Name}");

        if (IndexType != null)
        {
            outputContext.Write("[");
            outputContext.Write(IndexType);
            outputContext.Write(" ");
            outputContext.Write(IndexName);
            outputContext.Write("]");
        }

        if (Set == null && IndexType == null)
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
        else if (Get.StatementCount == 0 && 
                 Set is { StatementCount: 0 })
        {
            if ((Set.Modifiers & ComponentModifier.Private) == ComponentModifier.Private)
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
        
        outputContext.WriteIndentedLine($"/// <summary>");
        outputContext.WriteIndentedLine($"/// {Comment}");
        outputContext.WriteIndentedLine($"/// </summary>");
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