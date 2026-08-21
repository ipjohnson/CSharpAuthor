using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using CSharpAuthor.Profiles;

namespace CSharpAuthor;

/// <summary>
/// Which of the four type declarations a <see cref="ClassDefinition"/> writes.
/// </summary>
/// <remarks>
/// One class covers all four because they differ in a keyword and in what the language allows
/// inside them, not in how they are built. Everything but <see cref="Class"/> has a minimum
/// language version, and none of them has a downlevel: a record written as a class is a type with
/// different equality, which compiles.
/// </remarks>
public enum ClassKeyword
{
    /// <summary><c>class</c>.</summary>
    Class,

    /// <summary><c>record</c>. C# 9.</summary>
    Record,

    /// <summary><c>struct</c>.</summary>
    Struct,

    /// <summary><c>record struct</c>. C# 10.</summary>
    RecordStruct,

    /// <summary>
    /// A C# 15 union - <c>public union Shape(Circle, Square);</c>.
    /// </summary>
    /// <remarks>
    /// Declared entirely in its header: the cases are the primary constructor's parameters, written
    /// as bare types with no names, and the compiler synthesises a constructor and an implicit
    /// conversion per case plus a public <c>object? Value</c>. Add the cases with
    /// <see cref="ClassDefinition.AddUnionCase"/>, which is the same primary constructor every other
    /// keyword uses with the parameter names left off.
    ///
    /// A union has no body, so <see cref="ClassDefinition.TerminateWithSemicolon"/> is set for you
    /// when this keyword is chosen. It stays settable, because a union may declare members of its
    /// own and one that does needs braces.
    /// </remarks>
    Union
}

/// <summary>
/// A class, struct, record or record struct, and everything declared in it.
/// </summary>
/// <remarks>
/// <para>
/// The workhorse of the library. Members are added through the <c>Add*</c> methods, each of which
/// returns the member so it can be configured further:
/// </para>
/// <example>
/// <code>
/// var greeter = file.AddClass("Greeter");
/// greeter.AddField(typeof(string), "_name").Modifiers =
///     ComponentModifier.Private | ComponentModifier.Readonly;
/// greeter.AddProperty(typeof(string), "Name").Set = null;
/// greeter.AddMethod("Greet").Return("_name");
/// </code>
/// </example>
/// <para>
/// Members are written grouped by kind rather than in the order they were added - fields,
/// constructors, properties, events, methods, then nested types - so a generator that discovers
/// members in whatever order its input arrives in still emits a file that reads like one a person
/// wrote.
/// </para>
/// </remarks>
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

    /// <summary>
    /// A type declaration named <paramref name="name"/>. Prefer
    /// <see cref="CSharpFileDefinition.AddClass"/>, which builds one and attaches it to a file.
    /// </summary>
    /// <remarks>
    /// Constructing one directly is for a component that will be attached with
    /// <see cref="AddComponent"/> or <see cref="CSharpFileDefinition.AddComponent"/> - a type built
    /// by a helper that does not know what file it will land in.
    /// </remarks>
    public ClassDefinition(string name)
    {
        Name = name;
    }

    /// <summary>
    /// The declared name, escaped with <c>@</c> if it is a keyword.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Which of <c>class</c>, <c>struct</c>, <c>record</c> and <c>record struct</c> this declares.
    /// </summary>
    /// <remarks>
    /// <see cref="CSharpFileDefinition.AddRecord"/> sets this to <see cref="ClassKeyword.Record"/>;
    /// everything else arrives as a class and is changed here.
    /// </remarks>
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
    /// Whether a struct is declared <c>ref struct</c> - stack-only, and enforced by the compiler.
    /// </summary>
    /// <remarks>
    /// Ignored unless <see cref="TypeKeyword"/> is a struct. C# 7.2, and one of the features with
    /// no downlevel: dropping <c>ref</c> gives a type that compiles and can be boxed, captured and
    /// put on the heap - every restriction the caller asked for, silently removed. Below C# 7.2
    /// this is a capability violation rather than a formatting decision.
    /// </remarks>
    public bool IsRefStruct { get; set; }

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

    /// <summary>
    /// A type parameter given as a type - for a parameter that carries variance or that another
    /// part of the generator already built.
    /// </summary>
    /// <remarks>
    /// <see cref="AddGenericParameter(string)"/> is the one to reach for; this is the same call
    /// with the <see cref="TypeParameterDefinition"/> made by hand.
    /// </remarks>
    public ClassDefinition AddGenericParameter(ITypeDefinition typeDefinition)
    {
        _genericParameters.Add(typeDefinition);

        return this;
    }

    /// <summary>
    /// Adds a type parameter by name, for the common case of an unbound one.
    /// </summary>
    /// <remarks>
    /// <example>
    /// <code>
    /// var box = file.AddClass("Box");
    /// box.AddGenericParameter("T");
    /// box.AddConstraint("T").DefaultConstructor();
    /// </code>
    /// which is <c>public class Box&lt;T&gt; where T : new()</c>.
    /// </example>
    /// The name is what the members refer to, so a method returning <c>T</c> asks for
    /// <c>new TypeParameterDefinition("T")</c> rather than for a real type.
    /// </remarks>
    public ClassDefinition AddGenericParameter(string name)
    {
        return AddGenericParameter(new TypeParameterDefinition(name));
    }

    /// <summary>
    /// How many fields have been added, for a caller deciding whether a constructor is worth
    /// writing.
    /// </summary>
    public int FieldCount => _fields.Count;

    /// <summary>The constructors declared on this type, in the order they were added.</summary>
    public IReadOnlyList<ConstructorDefinition> Constructors => _constructors;

    /// <summary>The methods declared on this type, in the order they were added.</summary>
    public IReadOnlyList<MethodDefinition> Methods => _methods;

    /// <summary>The properties declared on this type, in the order they were added.</summary>
    public IReadOnlyList<PropertyDefinition> Properties => _properties;

    /// <summary>The fields declared on this type, in the order they were added.</summary>
    public IReadOnlyList<FieldDefinition> Fields => _fields;

    /// <summary>
    /// Adds an already-built member, sorting it into the same list the matching <c>Add*</c> method
    /// would have put it in.
    /// </summary>
    /// <remarks>
    /// For a member built somewhere else - by a helper that composes a property and its backing
    /// field together, say. Anything this does not recognise is kept as-is and written last, after
    /// the nested types.
    /// </remarks>
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

    /// <summary>
    /// Every member of this type that has a name, in the order they are written.
    /// </summary>
    /// <remarks>
    /// For inspecting a type built elsewhere - checking whether a member already exists before
    /// adding one, say. Unnamed components added through <see cref="AddComponent"/> are left out,
    /// because there is nothing to identify them by.
    /// </remarks>
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

    /// <summary>
    /// A type nested in this one: <c>public class Outer { public class Inner { } }</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nested types are written after every other member, which is where a reader expects them.
    /// </para>
    /// <para>
    /// This declares the nesting; it does not make the nested type <em>nameable</em> from
    /// elsewhere. A reference to it from another file has to be built with
    /// <see cref="TypeDefinition.GetNested"/>, which is what keeps it written as <c>Outer.Inner</c>
    /// rather than as a bare <c>Inner</c> that resolves to something else or to nothing.
    /// </para>
    /// </remarks>
    public ClassDefinition AddClass(string name)
    {
        var classDefinition = new ClassDefinition(name);

        _classes.Add(classDefinition);

        return classDefinition;
    }

    /// <summary>
    /// An interface nested in this type.
    /// </summary>
    /// <remarks>
    /// Written after the nested classes, with the other components this class has no list of its
    /// own for.
    /// </remarks>
    public InterfaceDefinition AddInterface(string name)
    {
        var interfaceDefinition = new InterfaceDefinition(name);

        _otherComponents.Add(interfaceDefinition);

        return interfaceDefinition;
    }

    /// <summary>
    /// An enum nested in this type - for one that is meaningless outside it.
    /// </summary>
    public EnumDefinition AddEnum(string name)
    {
        var enumDefinition = new EnumDefinition(name);
        _otherComponents.Add(enumDefinition);
        return enumDefinition;
    }

    /// <inheritdoc cref="AddProperty(ITypeDefinition, string)" />
    /// <remarks>
    /// <para>
    /// The overload for a type this generator can name at compile time - <c>typeof(string)</c>,
    /// <c>typeof(List&lt;int&gt;)</c>. It is <see cref="TypeDefinition.Get(Type)"/> applied for you,
    /// so it cannot express anything the other overload cannot.
    /// </para>
    /// <para>
    /// Reach for <see cref="AddProperty(ITypeDefinition, string)"/> when the type does not exist as
    /// a <see cref="Type"/>: a type this generator is also emitting, a type read out of a Roslyn
    /// symbol, or one that needed <see cref="ITypeDefinition.MakeNullable"/> or
    /// <see cref="ITypeDefinition.MakeArray()"/> applied to it - none of which a
    /// <see cref="Type"/> can carry through to the emitter.
    /// </para>
    /// </remarks>
    public PropertyDefinition AddProperty(Type type, string fieldName)
    {
        return AddProperty(TypeDefinition.Get(type), fieldName);
    }

    /// <inheritdoc cref="AddEvent(ITypeDefinition, string)" />
    public EventDefinition AddEvent(Type handlerType, string name)
    {
        return AddEvent(TypeDefinition.Get(handlerType), name);
    }

    /// <summary>
    /// An event: <c>public event Action&lt;string&gt; Greeted;</c>.
    /// </summary>
    /// <remarks>
    /// <paramref name="handlerType"/> is the delegate type, which is usually
    /// <see cref="TypeDefinition.Action"/> or an <c>EventHandler</c>. Field-like only - there is no
    /// <c>add</c>/<c>remove</c> accessor form here; a type that needs one declares it through
    /// <see cref="AddComponent"/>.
    /// </remarks>
    public EventDefinition AddEvent(ITypeDefinition handlerType, string name)
    {
        var eventDefinition = new EventDefinition(handlerType, name);

        _events.Add(eventDefinition);

        return eventDefinition;
    }

    /// <summary>
    /// A property. An auto-property unless the accessors are given statements:
    /// <c>public string Name { get; set; }</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns the property, which is where its shape is decided -
    /// <see cref="PropertyDefinition.Set"/> set to null for a get-only property,
    /// <see cref="PropertyMethodDefinition.IsInit"/> for <c>init</c>,
    /// <see cref="PropertyDefinition.DefaultValue"/> for an initialiser, statements on
    /// <see cref="PropertyDefinition.Get"/> for a full body.
    /// </para>
    /// <para>
    /// <strong>A property named <c>this</c> with an index is an indexer</strong>, and it is the one
    /// name this library treats as a keyword rather than as an identifier. See
    /// <see cref="PropertyDefinition"/>.
    /// </para>
    /// <para>
    /// Unlike <see cref="AddField(ITypeDefinition, string)"/>, a name already in use is not
    /// rejected: adding <c>Name</c> twice writes the declaration twice, and the compiler reports
    /// CS0102 in the generated file rather than this reporting it here.
    /// </para>
    /// </remarks>
    public PropertyDefinition AddProperty(ITypeDefinition type, string fieldName)
    {
        var propertyDefinition = new PropertyDefinition(type, fieldName);

        _properties.Add(propertyDefinition);

        return propertyDefinition;
    }

    /// <inheritdoc cref="AddField(ITypeDefinition, string)" />
    public FieldDefinition AddField(Type type, string fieldName)
    {
        return AddField(TypeDefinition.Get(type), fieldName);
    }

    /// <summary>
    /// A field: <c>private readonly string _name;</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A field is the one declaration in this library that defaults to <c>private</c> rather than
    /// <c>public</c>, because that is what a field almost always is. Add
    /// <see cref="ComponentModifier.Readonly"/> and <see cref="ComponentModifier.Static"/> through
    /// <see cref="BaseOutputComponent.Modifiers"/>; they are written in that order whichever order
    /// the flags were set in.
    /// </para>
    /// <example>
    /// <code>
    /// var items = greeter.AddField(TypeDefinition.List(typeof(string)), "_items");
    /// items.Modifiers = ComponentModifier.Private | ComponentModifier.Readonly;
    /// items.InitializeValue = SyntaxHelpers.New(TypeDefinition.List(typeof(string)));
    /// </code>
    /// which is <c>private readonly List&lt;string&gt; _items = new List&lt;string&gt;();</c>.
    /// </example>
    /// <para>
    /// Throws <see cref="ArgumentException"/> if a field of that name is already declared. This is
    /// the only <c>Add*</c> that checks, and it checks because a duplicate field is usually a
    /// generator visiting the same input twice - a mistake worth failing on rather than emitting.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">A field named <paramref name="fieldName"/> already
    /// exists on this type.</exception>
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

    /// <summary>
    /// A method. <c>public void</c> with an empty body until it is told otherwise.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything about the method is set on what this returns: the return type through
    /// <see cref="MethodDefinition.SetReturnType(Type)"/>, parameters through
    /// <see cref="MethodDefinition.AddParameter(Type, string)"/>, and the body through the
    /// statement methods it inherits from <see cref="BaseBlockDefinition"/>.
    /// </para>
    /// <example>
    /// <code>
    /// var greet = greeter.AddMethod("Greet");
    /// greet.SetReturnType(typeof(string));
    /// greet.AddParameter(typeof(string), "name");
    /// greet.Return("name");
    /// </code>
    /// which is
    /// <code>
    /// public string Greet(string name)
    /// {
    ///     return name;
    /// }
    /// </code>
    /// </example>
    /// <para>
    /// Overloads are ordinary: add two methods of the same name with different parameters. Nothing
    /// checks that they differ, the same way nothing else here validates what it is handed.
    /// </para>
    /// </remarks>
    public MethodDefinition AddMethod(string method)
    {
        var definition = new MethodDefinition(method);

        _methods.Add(definition);

        return definition;
    }

    /// <summary>
    /// A base type or an implemented interface: <c>public class Greeter : IDisposable</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// C# writes the base class first and the interfaces after it, and that is the caller's to get
    /// right - these are written in the order they were added. There is no <see cref="Type"/>
    /// overload: pass <c>TypeDefinition.Get(typeof(IDisposable))</c>, or the definition of a type
    /// this generator is emitting.
    /// </para>
    /// <para>
    /// Returns the class rather than the base type, so calls chain:
    /// <c>definition.AddBaseType(a).AddBaseType(b)</c>.
    /// </para>
    /// <para>
    /// The same type added twice is added once. That is deduplication on the type alone, so it also
    /// discards a later call carrying constructor arguments - see
    /// <see cref="AddBaseType(ITypeDefinition, IOutputComponent[])"/>, which is the overload that
    /// has them.
    /// </para>
    /// </remarks>
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
    /// <example>
    /// <code>
    /// var dog = file.AddRecord("Dog");
    /// dog.TerminateWithSemicolon = true;
    /// var ctor = dog.AddConstructor();
    /// ctor.IsPrimary = true;
    /// ctor.AddParameter(typeof(string), "Id");
    /// ctor.AddParameter(typeof(string), "Breed");
    /// dog.AddBaseType(petType, new CodeOutputComponent("Id") { Indented = false });
    /// </code>
    /// which is <c>public record Dog(string Id, string Breed) : Pet(Id);</c>.
    /// </example>
    /// <para>
    /// Attach the arguments on the <em>first</em> call for a given base type. A second call naming a
    /// type already in the list is discarded whatever it carries, so
    /// <c>AddBaseType(pet); AddBaseType(pet, id);</c> writes <c>: Pet</c> with no arguments at all.
    /// </para>
    /// </remarks>
    public ClassDefinition AddBaseType(ITypeDefinition typeDefinition, params IOutputComponent[] arguments)
    {
        foreach (var existing in _baseTypes)
        {
            // Deduplicated on the type alone, so a second call carrying constructor arguments is
            // discarded and a record loses the arguments its base needs - CS7036, adversary #32.
            // ClassDefinitionTests.BaseTypeArgumentTests.ABaseTypeIsNotAddedTwice pins this exact
            // reading, so it stays. See docs/migration-v1-v2.md.
            if (existing.Type.Equals(typeDefinition))
            {
                return this;
            }
        }

        _baseTypes.Add(new BaseTypeReference(typeDefinition, arguments));

        return this;
    }

    /// <summary>
    /// A constructor, named after the type. Optionally with a <c>: base(...)</c> or
    /// <c>: this(...)</c> initialiser.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="ConstructorDefinition"/> is a <see cref="MethodDefinition"/> with no return
    /// type, so parameters and a body are added the same way.
    /// </para>
    /// <example>
    /// <code>
    /// var ctor = greeter.AddConstructor(SyntaxHelpers.Base("name"));
    /// ctor.AddParameter(typeof(string), "name");
    /// </code>
    /// which is
    /// <code>
    /// public Greeter(string name)
    ///      : base(name)
    /// {
    /// }
    /// </code>
    /// </example>
    /// <para>
    /// Set <see cref="ConstructorDefinition.IsPrimary"/> on what this returns to move the parameter
    /// list into the type header instead - the <c>record Pet(string Id)</c> form. A type has at most
    /// one of those, and the primary constructor writes no member of its own.
    /// </para>
    /// <para>
    /// <see cref="ComponentModifier.Static"/> writes a static constructor, which takes no
    /// accessibility keyword and no parameters.
    /// </para>
    /// </remarks>
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
    /// A union's cases are the primary constructor's parameters written as bare types, so this
    /// creates that constructor on first use and appends to it afterwards. The parameter is given a
    /// name because a parameter has one; it is not written for a union, and the compiler names the
    /// synthesised members itself.
    ///
    /// Order is the order cases are added, which is the order they appear in the declaration - and
    /// on a union that is the order a <c>switch</c> over the value is checked in.
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

        primary.AddParameter(
            caseType,
            "case" + primary.Parameters.Count.ToString(CultureInfo.InvariantCulture));

        return this;
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        // Before the signature is written, so a #error directive lands on a line of its own.
        RequireCapabilities(outputContext);

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
        // Asked before the declaration line starts, because a diagnostic is a line of its own and
        // cannot be inserted into a half-written one.
        //
        // The keyword is then written whatever the answer. There is no downlevel: `internal` is the
        // nearest thing C# has and it publishes the type to the whole assembly, which is the silent
        // widening this library exists to refuse. Writing `file` into a compilation that cannot
        // parse it fails loudly, next to the diagnostic that says why.
        if ((Modifiers & ComponentModifier.File) == ComponentModifier.File)
        {
            outputContext.EmitSession().Require(LanguageFeature.FileLocalTypes, outputContext, Name);
        }

        outputContext.Write(outputContext.IndentString);

        var accessModifier = GetAccessModifier(KeyWords.Public);

        if (!string.IsNullOrEmpty(accessModifier))
        {
            outputContext.Write(accessModifier);
            outputContext.WriteSpace();
        }

        // One chain of `else if` used to mean one modifier: `abstract partial` kept both because
        // partial was tested separately, but `static abstract` lost the abstract, and `readonly`
        // was never written, so a readonly struct came out mutable.
        outputContext.Write(
            Modifiers.GetModifierKeywords(ComponentModifierExtensions.TypeModifiers));

        if (IsRefStruct && IsStruct && outputContext.EmitProfile().Supports(LanguageFeature.RefStructs))
        {
            outputContext.Write(KeyWords.Ref);
            outputContext.WriteSpace();
        }

        outputContext.Write(GetTypeKeywordString());
        outputContext.WriteSpace();

        outputContext.Write(CSharpIdentifier.Escape(Name));

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
            //
            // 1.2.0 called AddImportNamespace here beside the write. It is gone: Write records the
            // type unrendered and the namespace is derived from it at serialization (invariant 1).
            if (TypeKeyword == ClassKeyword.Union)
            {
                outputContext.Write(primary.Parameters[i].TypeDefinition);
            }
            else
            {
                primary.Parameters[i].WriteWithSignature(outputContext);
            }
        }

        outputContext.Write(")");
    }

    private bool IsStruct =>
        TypeKeyword == ClassKeyword.Struct || TypeKeyword == ClassKeyword.RecordStruct;

    private bool HasPrimaryConstructor
    {
        get
        {
            foreach (var constructor in _constructors)
            {
                if (constructor.IsPrimary)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Everything about this declaration that the target language version has to have.
    /// </summary>
    /// <remarks>
    /// Every one of these is demanded rather than asked about, because this writer has no
    /// alternative form for any of them. A primary constructor could in principle be written out
    /// as fields and a constructor - that is why the capability table calls it free - but nothing
    /// here does that, and dropping the parameters would give a type with no way to construct it.
    /// A silent "near enough" is the failure this library exists to remove.
    /// </remarks>
    private void RequireCapabilities(IOutputContext outputContext)
    {
        var session = outputContext.EmitSession();

        if (IsRefStruct && IsStruct)
        {
            session.Require(LanguageFeature.RefStructs, outputContext, Name);
        }

        if (TypeKeyword == ClassKeyword.Record)
        {
            session.Require(LanguageFeature.Records, outputContext, Name);
        }
        else if (TypeKeyword == ClassKeyword.RecordStruct)
        {
            session.Require(LanguageFeature.RecordStructs, outputContext, Name);
        }
        else if (TypeKeyword == ClassKeyword.Union)
        {
            session.Require(LanguageFeature.Unions, outputContext, Name);
        }
        else if (HasPrimaryConstructor)
        {
            // A record carries its positional parameters from C# 9; a class or struct only from
            // C# 12.
            session.Require(LanguageFeature.PrimaryConstructors, outputContext, Name);
        }
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