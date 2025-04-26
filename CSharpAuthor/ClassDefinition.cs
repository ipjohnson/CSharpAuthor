using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;

namespace CSharpAuthor;

public class ClassDefinition : BaseOutputComponent, IConstructContainer, INamedComponent
{
    private readonly List<ITypeDefinition> _baseTypes = new();
    private readonly List<FieldDefinition> _fields = new();
    private readonly List<ConstructorDefinition> _constructors = new();
    private readonly List<MethodDefinition> _methods = new();
    private readonly List<PropertyDefinition> _properties = new();
    private readonly List<ClassDefinition> _classes = new();
    private readonly List<IOutputComponent> _otherComponents = new();

    public ClassDefinition(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public int FieldCount => _fields.Count;

    public IReadOnlyList<ConstructorDefinition> Constructors => _constructors;

    public IReadOnlyList<MethodDefinition> Methods => _methods;

    public IReadOnlyList<PropertyDefinition> Properties => _properties;

    public IReadOnlyList<FieldDefinition> Fields => _fields;

    public void AddComponent(IOutputComponent outputComponent)
    {
        switch (outputComponent)
        {
            case ClassDefinition classDefinition:
                _classes.Add(classDefinition);
                break;
            case PropertyDefinition propertyDefinition:
                _properties.Add(propertyDefinition);
                break;
            case FieldDefinition fieldDefinition:
                _fields.Add(fieldDefinition);
                break;
            case ConstructorDefinition constructorDefinition:
                _constructors.Add(constructorDefinition);
                break;
            case MethodDefinition methodDefinition:
                _methods.Add(methodDefinition);
                break;
            default:
                _otherComponents.Add(outputComponent);
                break;
        }
    }

    public IEnumerable<IOutputComponent> GetAllNamedComponents()
    {
        if (_fields.Count > 0)
        {
            foreach (var field in _fields)
            {
                yield return field;
            }
        }

        if (_methods.Count > 0)
        {
            foreach (var method in _methods)
            {
                yield return method;
            }
        }

        if (_properties.Count > 0)
        {
            foreach (var property in _properties)
            {
                yield return property;
            }
        }

        if (_classes.Count > 0)
        {
            foreach (var classDefinition in _classes)
            {
                yield return classDefinition;
            }
        }

        if (_otherComponents.Count > 0)
        {
            foreach (var outputComponent in _otherComponents)
            {
                if (outputComponent is INamedComponent)
                {
                    yield return outputComponent;
                }
            }
        }
    }

    public ClassDefinition AddClass(string name)
    {
        var classDefinition = new ClassDefinition(name);

        _classes.Add(classDefinition);

        return classDefinition;
    }

    public InterfaceDefinition AddInterface(string name)
    {
        var interfaceDefinition = new InterfaceDefinition(name);
        
        _otherComponents.Add(interfaceDefinition);
        
        return interfaceDefinition;
    }

    public EnumDefinition AddEnum(string name)
    {
        var enumDefinition = new EnumDefinition(name);
        _otherComponents.Add(enumDefinition);
        return enumDefinition;
    }

    public PropertyDefinition AddProperty(Type type, string fieldName)
    {
        return AddProperty(TypeDefinition.Get(type), fieldName);
    }

    public PropertyDefinition AddProperty(ITypeDefinition type, string fieldName)
    {
        var propertyDefinition = new PropertyDefinition(type, fieldName);

        _properties.Add(propertyDefinition);

        return propertyDefinition;
    }

    public FieldDefinition AddField(Type type, string fieldName)
    {
        return AddField(TypeDefinition.Get(type), fieldName);
    }

    public FieldDefinition AddField(ITypeDefinition typeDefinition, string fieldName)
    {
        if (_fields.Any(f => f.Name == fieldName))
        {
            throw new ArgumentException($"{fieldName} field already exists in class");
        }

        var definition = new FieldDefinition(typeDefinition, fieldName);

        _fields.Add(definition);

        return definition;
    }

    public MethodDefinition AddMethod(string method)
    {
        var definition = new MethodDefinition(method);

        _methods.Add(definition);

        return definition;
    }

    public ClassDefinition AddBaseType(ITypeDefinition typeDefinition)
    {
        if (!_baseTypes.Contains(typeDefinition))
        {
            _baseTypes.Add(typeDefinition);
        }

        return this;
    }

    public ConstructorDefinition AddConstructor(IOutputComponent? baseComponent = null)
    {
        var definition = new ConstructorDefinition(Name, baseComponent);

        _constructors.Add(definition);

        return definition;
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        WriteClassOpening(outputContext);

        ApplyAllComponents(component => component.WriteOutput(outputContext), outputContext);

        WriteClassClosing(outputContext);
    }

    protected override void WriteComment(IOutputContext outputContext)
    {
        if (string.IsNullOrWhiteSpace(Comment))
        {
            return;
        }
        
        outputContext.WriteIndentedLine("/// <summary>");
        outputContext.WriteIndentedLine($"/// {Comment}");
        outputContext.WriteIndentedLine("/// </summary>");
    }

    private void ApplyAllComponents(Action<IOutputComponent> componentAction, IOutputContext outputContext)
    {
        foreach (var field in _fields)
        {
            componentAction(field);
        }

        foreach (var constructor in _constructors)
        {
            outputContext.WriteLine();

            componentAction(constructor);
        }
        
        WriteMemberComponents(
            componentAction,
            outputContext,
            _properties,
            method => method.Modifiers.HasFlag(ComponentModifier.Public));
        
        WriteMemberComponents(
            componentAction, 
            outputContext, 
            _methods,
            m => m.Modifiers.HasFlag(ComponentModifier.Public));
        
        WriteMemberComponents(
            componentAction,
            outputContext,
            _properties,
            method => !method.Modifiers.HasFlag(ComponentModifier.Public));
        
        WriteMemberComponents(
            componentAction, 
            outputContext, 
            _methods,
            m => !m.Modifiers.HasFlag(ComponentModifier.Public));
        
        foreach (var classDefinition in _classes)
        {
            outputContext.WriteLine();

            componentAction(classDefinition);
        }

        foreach (var outputComponent in _otherComponents)
        {
            outputContext.WriteLine();

            componentAction(outputComponent);
        }
    }

    private void WriteMemberComponents(
        Action<IOutputComponent> componentAction, 
        IOutputContext outputContext,
        IEnumerable<BaseOutputComponent> components,
        Func<BaseOutputComponent,bool> filter) {
        
        foreach (var component in components)
        {
            if (filter(component))
            {
                continue;
            }
            
            outputContext.WriteLine();

            componentAction(component);
        }
    }

    private void WriteClassClosing(IOutputContext outputContext)
    {
        outputContext.CloseScope();
    }

    private void WriteClassOpening(IOutputContext outputContext)
    {
        WriteClassSignature(outputContext);
        outputContext.OpenScope();
    }

    private void WriteClassSignature(IOutputContext outputContext)
    {
        outputContext.Write(outputContext.IndentString);

        var accessModifier = GetAccessModifier(KeyWords.Public);

        if (!string.IsNullOrEmpty(accessModifier))
        {
            outputContext.Write(accessModifier);
            outputContext.WriteSpace();
        }

        if ((Modifiers & ComponentModifier.Static) == ComponentModifier.Static)
        {
            outputContext.Write(KeyWords.Static);
            outputContext.WriteSpace();
        }
        else if ((Modifiers & ComponentModifier.Abstract) == ComponentModifier.Abstract)
        {
            outputContext.Write(KeyWords.Abstract);
            outputContext.WriteSpace();
        }

        if ((Modifiers & ComponentModifier.Partial) == ComponentModifier.Partial)
        {
            outputContext.Write(KeyWords.Partial);
            outputContext.WriteSpace();
        }

        outputContext.Write(KeyWords.Class);
        outputContext.WriteSpace();

        outputContext.Write(Name);

        if (_baseTypes.Count > 0)
        {
            outputContext.Write(" : ");

            _baseTypes.OutputCommaSeparatedList(outputContext);
        }

        outputContext.WriteLine();
    }
}