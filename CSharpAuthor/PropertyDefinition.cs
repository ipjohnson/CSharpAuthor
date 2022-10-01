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
                outputContext.WriteLine(" { get; private set; }");
            }
            else if ((Set.Modifiers & ComponentModifier.Protected) == ComponentModifier.Protected)
            {
                outputContext.WriteLine(" { get; protected set; }");
            }
            else
            {
                outputContext.WriteLine(" { get; set; }");
            }

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