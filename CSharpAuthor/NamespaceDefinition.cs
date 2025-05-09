using System.Collections.Generic;
using System.Linq;

namespace CSharpAuthor;

public class NamespaceDefinition : BaseOutputComponent, IConstructContainer
{
    private readonly string _namespace;
    private readonly List<IOutputComponent> _outputComponents = new ();

    public NamespaceDefinition(string ns = "")
    {
        _namespace = ns;
    }

    public NamespaceDefinition AddNamespace(string @namespace)
    {
        var namespaceDefinition = new NamespaceDefinition(@namespace);
        
        _outputComponents.Add(namespaceDefinition);
        
        return namespaceDefinition;
    }
    
    public IEnumerable<IOutputComponent> GetAllNamedComponents()
    {
        return _outputComponents.Where(x => x is INamedComponent);
    }

    public ClassDefinition AddClass(string name)
    {
        var classDefinition = new ClassDefinition(name);

        _outputComponents.Add(classDefinition);

        return classDefinition;
    }

    public EnumDefinition AddEnum(string name)
    {
        var enumDefinition = new EnumDefinition(name);

        _outputComponents.Add(enumDefinition);

        return enumDefinition;
    }

    public InterfaceDefinition AddInterface(string interfaceName)
    {
        var interfaceDefinition = new InterfaceDefinition(interfaceName);

        _outputComponents.Add(interfaceDefinition);

        return interfaceDefinition;
    }

    public void AddComponent(IOutputComponent component)
    {
        _outputComponents.Add(component);
    }
    

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        if (!string.IsNullOrEmpty(_namespace))
        {
            WriteNamespaceOpen(outputContext);
        }

        var newLine = false;

        foreach (var outputComponent in _outputComponents)
        {
            if (newLine)
            {
                outputContext.WriteLine();
            }
            else
            {
                newLine = true;
            }

            outputComponent.WriteOutput(outputContext);
        }

        if (!string.IsNullOrEmpty(_namespace))
        {
            WriteNamespaceClose(outputContext);
        }
    }

    private void WriteNamespaceOpen(IOutputContext outputContext)
    {
        outputContext.WriteIndentedLine("namespace " + _namespace);
        outputContext.OpenScope();
    }
    
    private void WriteNamespaceClose(IOutputContext outputContext)
    {
        outputContext.CloseScope();
    }
}