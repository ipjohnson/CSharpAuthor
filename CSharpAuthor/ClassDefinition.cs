using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;

namespace CSharpAuthor;

public enum ClassKeyword
{
    Class,
    Record,
    Struct,
    RecordStruct,

    /// <summary>
    /// A C# 15 union - <c>public union Shape(Circle, Square);</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Declared entirely in its header: the cases are the primary constructor's parameters, written
    /// as bare types with no names, and the compiler synthesises a constructor and an implicit
    /// conversion per case plus a public <c>object? Value</c>. Add the cases with
    /// <see cref="ClassDefinition.AddUnionCase"/>, which is the same primary constructor every other
    /// keyword uses with the parameter names left off.
    /// </para>
    /// <para>
    /// A union has no body, so <see cref="ClassDefinition.TerminateWithSemicolon"/> is set for you
    /// when this keyword is chosen. It stays settable, because a union may declare members of its
    /// own and one that does needs braces.
    /// </para>
    /// </remarks>
    Union
}

public class ClassDefinition : BaseOutputComponent, IConstructContainer, INamedComponent
{
    private readonly List<BaseTypeReference> _baseTypes = new();
    private readonly List<FieldDefinition> _fields = new();
    private readonly List<ConstructorDefinition> _constructors = new();
    private readonly List<MethodDefinition> _methods = new();
    private readonly List<PropertyDefinition> _properties = new();
    private readonly List<ClassDefinition> _classes = new();
    private readonly List<IOutputComponent> _otherComponents = new();
    private readonly List<ITypeDefinition> _genericParameters = new();
    private readonly List<EventDefinition> _events = new();
    private readonly List<ConstraintDefinition> _constraints = new();

    public ClassDefinition(string name)
    {
        Name = name;
    }

    public string Name { get; }

    /// <summary>
    /// The keyword this type is declared with.
    /// </summary>
    /// <remarks>
    /// Choosing <see cref="ClassKeyword.Union"/> turns on <see cref="TerminateWithSemicolon"/>,
    /// because a union declares its cases in its header and has nothing to put in a body. It stays
    /// settable afterwards, for a union that does declare members of its own.
    /// </remarks>
    public ClassKeyword TypeKeyword
    {
        get => _typeKeyword;
        set
        {
            _typeKeyword = value;

            if (value == ClassKeyword.Union)
            {
                TerminateWithSemicolon = true;
            }
        }
    }

    private ClassKeyword _typeKeyword = ClassKeyword.Class;

    /// <summary>
    /// Whether the declaration is terminated with <c>;</c> rather than a body.
    /// </summary>
    /// <remarks>
    /// For a type that declares everything in its header and has nothing else to say -
    /// <c>public partial record Pet(string Id);</c> - where an empty <c>{ }</c> would be legal but
    /// is not what anyone writes. Any members added are still written, so this is only correct on a
    /// type that has none; it is left as the caller's choice rather than inferred, because a type
    /// that is empty today and gains a member tomorrow should not silently change shape.
    /// </remarks>
    public bool TerminateWithSemicolon { get; set; }

    /// <summary>
    /// The type parameters this type is declared with, written as Name&lt;T, U&gt;.
    /// </summary>
    public IReadOnlyList<ITypeDefinition> GenericParameters => _genericParameters;

    /// <summary>
    /// The constraint clause, written after the base types: <c>where T : class, new()</c>.
    /// </summary>
    /// <remarks>
    /// A clause the caller has already rendered. <see cref="AddConstraint"/> builds one part by part
    /// instead, and the two can be used together — this is written first.
    /// </remarks>
    public IOutputComponent? WhereStatement { get; set; }

    /// <summary>
    /// The constraints declared through <see cref="AddConstraint"/>.
    /// </summary>
    public IReadOnlyList<ConstraintDefinition> Constraints => _constraints;

    /// <summary>
    /// Constrains one of this type's parameters, written after the base types.
    /// </summary>
    /// <remarks>
    /// Returns the constraint so its parts can be added in any order; they are written in the order
    /// C# requires. Calling this twice for one parameter returns the same constraint rather than
    /// declaring a second <c>where</c> for it, which would not compile.
    /// </remarks>
    public ConstraintDefinition AddConstraint(string typeParameter)
    {
        foreach (var existing in _constraints)
        {
            if (existing.TypeParameter == typeParameter)
            {
                return existing;
            }
        }

        var constraint = new ConstraintDefinition(typeParameter);

        _constraints.Add(constraint);

        return constraint;
    }

    public ClassDefinition AddGenericParameter(ITypeDefinition typeDefinition)
    {
        _genericParameters.Add(typeDefinition);

        return this;
    }

    /// <summary>
    /// Adds a type parameter by name, for the common case of an unbound one.
    /// </summary>
    public ClassDefinition AddGenericParameter(string name)
    {
        return AddGenericParameter(new TypeParameterDefinition(name));
    }

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
            case EventDefinition eventDefinition:
                _events.Add(eventDefinition);
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

        if (_events.Count > 0)
        {
            foreach (var eventDefinition in _events)
            {
                yield return eventDefinition;
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

    public EventDefinition AddEvent(Type handlerType, string name)
    {
        return AddEvent(TypeDefinition.Get(handlerType), name);
    }

    public EventDefinition AddEvent(ITypeDefinition handlerType, string name)
    {
        var eventDefinition = new EventDefinition(handlerType, name);

        _events.Add(eventDefinition);

        return eventDefinition;
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
        return AddBaseType(typeDefinition, Array.Empty<IOutputComponent>());
    }

    /// <summary>
    /// A base type, with the arguments its constructor is called with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For a positional record this is the only way to say <c>record Dog(string Id, string Breed) :
    /// Pet(Id)</c>. Without it a derived record could name its base but not pass anything to it, so
    /// any generator emitting an inheritance hierarchy had to abandon positional records entirely
    /// and fall back to init-only properties.
    /// </para>
    /// <para>
    /// C# allows the arguments on the base class only, and it has to come first in the list. That
    /// is the caller's to get right - this writes the arguments wherever they were attached.
    /// </para>
    /// </remarks>
    public ClassDefinition AddBaseType(ITypeDefinition typeDefinition, params IOutputComponent[] arguments)
    {
        foreach (var existing in _baseTypes)
        {
            if (existing.Type.Equals(typeDefinition))
            {
                return this;
            }
        }

        _baseTypes.Add(new BaseTypeReference(typeDefinition, arguments));

        return this;
    }

    public ConstructorDefinition AddConstructor(IOutputComponent? baseComponent = null)
    {
        var definition = new ConstructorDefinition(Name, baseComponent);

        _constructors.Add(definition);

        return definition;
    }

    /// <summary>
    /// Adds one case to a <see cref="ClassKeyword.Union"/> declaration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A union's cases are the primary constructor's parameters written as bare types, so this
    /// creates that constructor on first use and appends to it afterwards. The parameter is given a
    /// name because a parameter has one; it is not written for a union, and the compiler names the
    /// synthesised members itself.
    /// </para>
    /// <para>
    /// Order is the order cases are added, which is the order they appear in the declaration - and
    /// on a union that is the order a <c>switch</c> over the value is checked in.
    /// </para>
    /// </remarks>
    public ClassDefinition AddUnionCase(ITypeDefinition caseType)
    {
        ConstructorDefinition? primary = null;

        foreach (var constructor in _constructors)
        {
            if (constructor.IsPrimary)
            {
                primary = constructor;
                break;
            }
        }

        if (primary == null)
        {
            primary = AddConstructor();
            primary.IsPrimary = true;
        }

        primary.AddParameter(caseType, "case" + primary.Parameters.Count);

        return this;
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        if (TerminateWithSemicolon)
        {
            WriteClassSignature(outputContext, terminator: ";");
            return;
        }

        WriteClassOpening(outputContext);

        ApplyAllComponents(component => component.WriteOutput(outputContext), outputContext);

        WriteClassClosing(outputContext);
    }

    /// <summary>
    /// The type's summary, followed by a <c>&lt;param&gt;</c> for each documented primary
    /// constructor parameter.
    /// </summary>
    /// <remarks>
    /// A positional record declares its properties in its header, so <c>&lt;param&gt;</c> on the
    /// type is where documentation for them belongs - the same arrangement a method uses, and the
    /// only place the compiler will accept it. Without this a record's properties could not be
    /// documented at all, only the record itself.
    /// </remarks>
    protected override void WriteComment(IOutputContext outputContext)
    {
        if (string.IsNullOrWhiteSpace(Comment))
        {
            return;
        }

        DocumentationComment.WriteSummary(outputContext.WriteIndentedLine, Comment);

        foreach (var constructor in _constructors)
        {
            if (!constructor.IsPrimary)
            {
                continue;
            }

            foreach (var parameter in constructor.Parameters)
            {
                DocumentationComment.WriteElement(
                    outputContext.WriteIndentedLine,
                    "<param name=\"" + parameter.Name + "\">",
                    "</param>",
                    parameter.Comment);
            }

            break;
        }
    }

    private void ApplyAllComponents(Action<IOutputComponent> componentAction, IOutputContext outputContext)
    {
        foreach (var field in _fields)
        {
            componentAction(field);
        }

        foreach (var constructor in _constructors)
        {
            // The primary constructor is part of the type header, written by WriteClassSignature.
            if (constructor.IsPrimary)
            {
                continue;
            }

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
            _events,
            e => e.Modifiers.HasFlag(ComponentModifier.Public));

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
            _events,
            e => !e.Modifiers.HasFlag(ComponentModifier.Public));

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

    private void WriteClassSignature(IOutputContext outputContext, string? terminator = null)
    {
        outputContext.Write(outputContext.IndentString);

        var accessModifier = GetAccessModifier(KeyWords.Public);

        if (!string.IsNullOrEmpty(accessModifier))
        {
            outputContext.Write(accessModifier);
            outputContext.WriteSpace();
        }

        if ((Modifiers & ComponentModifier.Sealed) == ComponentModifier.Sealed)
        {
            outputContext.Write(KeyWords.Sealed);
            outputContext.WriteSpace();
        }
        else if ((Modifiers & ComponentModifier.Static) == ComponentModifier.Static)
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

        outputContext.Write(GetTypeKeywordString());
        outputContext.WriteSpace();

        outputContext.Write(Name);

        if (_genericParameters.Count > 0)
        {
            outputContext.Write("<");

            _genericParameters.OutputCommaSeparatedList(outputContext);

            outputContext.Write(">");
        }

        WritePrimaryConstructorParameters(outputContext);

        if (_baseTypes.Count > 0)
        {
            outputContext.Write(" : ");

            _baseTypes.OutputCommaSeparatedList(
                outputContext, (context, baseType) => baseType.WriteOutput(context));
        }

        // After the base types, which is where C# puts them.
        WhereStatement?.WriteOutput(outputContext);

        foreach (var constraint in _constraints)
        {
            if (constraint.IsEmpty)
            {
                continue;
            }

            outputContext.WriteSpace();

            constraint.WriteOutput(outputContext);
        }

        if (terminator != null)
        {
            outputContext.Write(terminator);
        }

        outputContext.WriteLine();
    }

    /// <summary>
    /// The primary constructor's parameters, written between the type name and its base types.
    /// </summary>
    /// <remarks>
    /// An empty list still writes <c>()</c>, because a primary constructor taking nothing is a
    /// different declaration from no primary constructor at all - and on a record it is what gives
    /// the type value equality with no members.
    /// </remarks>
    private void WritePrimaryConstructorParameters(IOutputContext outputContext)
    {
        ConstructorDefinition? primary = null;

        foreach (var constructor in _constructors)
        {
            if (constructor.IsPrimary)
            {
                primary = constructor;
                break;
            }
        }

        if (primary == null)
        {
            return;
        }

        outputContext.Write("(");

        for (var i = 0; i < primary.Parameters.Count; i++)
        {
            if (i > 0)
            {
                outputContext.Write(", ");
            }

            // A union's cases are types, not parameters - `union Shape(Circle, Square)`. Writing a
            // name after each would not compile, and the compiler names the members itself.
            if (TypeKeyword == ClassKeyword.Union)
            {
                outputContext.AddImportNamespace(primary.Parameters[i].TypeDefinition);
                outputContext.Write(primary.Parameters[i].TypeDefinition);
            }
            else
            {
                primary.Parameters[i].WriteWithSignature(outputContext);
            }
        }

        outputContext.Write(")");
    }

    private string GetTypeKeywordString() => TypeKeyword switch
    {
        ClassKeyword.Record => KeyWords.Record,
        ClassKeyword.Struct => "struct",
        ClassKeyword.RecordStruct => "record struct",
        ClassKeyword.Union => "union",
        _ => KeyWords.Class
    };
}