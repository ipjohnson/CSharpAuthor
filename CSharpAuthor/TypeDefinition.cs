using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CSharpAuthor;

/// <summary>
/// A non-generic type reference, and the factory for every other kind.
/// </summary>
/// <remarks>
/// <para>
/// The <c>Get</c> methods are the way in, and which one to use depends on whether the type exists
/// yet:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <see cref="Get(Type)"/> for a type this generator can name at compile time. It handles
/// keywords, arrays, closed generics and nesting.
/// </description></item>
/// <item><description>
/// <see cref="Get(string, string, bool, bool)"/> for one this generator is emitting, named by
/// namespace and name. Nothing checks that it exists.
/// </description></item>
/// <item><description>
/// <see cref="GetNested"/> for one declared inside another, so it writes as <c>Outer.Inner</c>.
/// </description></item>
/// <item><description>
/// <see cref="Task"/>, <see cref="IEnumerable"/>, <see cref="List"/>, <see cref="Action"/>,
/// <see cref="Func"/> and <see cref="IOptions"/> for the closed generics a generator reaches for
/// most, each of which also accepts an <see cref="ITypeDefinition"/> argument -
/// <c>typeof(List&lt;&gt;)</c> cannot be closed over a type that does not exist yet.
/// </description></item>
/// </list>
/// <para>
/// Shapes are asked for rather than constructed: <see cref="ITypeDefinition.MakeArray()"/>,
/// <see cref="ITypeDefinition.MakeNullable"/> and
/// <see cref="ITypeDefinitionExtensions.MakeArrayOfNullable"/> each return a new instance and leave
/// the original alone, so one definition can be shared across a whole generator run.
/// </para>
/// </remarks>
public class TypeDefinition : BaseTypeDefinition
{
    /// <summary>
    /// A type with the 1.x single array and nullable flags.
    /// </summary>
    /// <remarks>
    /// A pair of flags cannot say <c>int[][]</c>, <c>int[,]</c>, or the difference between
    /// <c>int?[]</c> and <c>int[]?</c>. Prefer <see cref="Get(Type)"/> or
    /// <see cref="Get(string, string, bool, bool)"/> followed by the shape calls.
    /// </remarks>
    public TypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string ns, string name, bool isArray, bool isNullable = false) : base(typeDefinitionEnum, ns, name,  isArray, isNullable)
    {

    }

    /// <summary>
    /// A type with array specifiers, outermost first (<c>[2, 1]</c> is <c>Name[,][]</c>), one
    /// nullable flag for the type itself, and optionally the type it is declared inside.
    /// </summary>
    /// <remarks>
    /// The flag annotates the outermost level only - the array, on an array type. Use the overload
    /// taking a list of annotations to say where each <c>?</c> goes.
    /// </remarks>
    public TypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string ns, string name, IReadOnlyList<int>? arrayRanks, bool isNullable = false, ITypeDefinition? containingType = null)
        : base(typeDefinitionEnum, ns, name, arrayRanks, isNullable, containingType)
    {

    }

    /// <summary>
    /// A type with array specifiers and an annotation for each level, outermost first, then one for
    /// the element - <c>[1]</c> with <c>[false, true]</c> is <c>Name?[]</c>.
    /// </summary>
    /// <remarks>
    /// Every parameter is required. An overload that defaulted the annotations would be reachable
    /// by the same call that reaches the <c>bool</c> one above, and the two mean different things.
    /// </remarks>
    public TypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string ns, string name, IReadOnlyList<int>? arrayRanks, IReadOnlyList<bool>? nullableAnnotations, ITypeDefinition? containingType)
        : base(typeDefinitionEnum, ns, name, arrayRanks, nullableAnnotations, containingType)
    {

    }

    /// <inheritdoc />
    public override IEnumerable<string> KnownNamespaces
    {
        get
        {
            if (ContainingType != null)
            {
                foreach (var knownNamespace in ContainingType.KnownNamespaces)
                {
                    yield return knownNamespace;
                }
            }

            yield return Namespace;
        }
    }

    /// <inheritdoc />
    public override void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName)
    {

        if (Name == "Void" && Namespace == "System")
        {
            builder.Append("void");
            return;
        }

        WriteQualifier(builder, typeOutputMode);

        builder.Append(WrittenName());

        WriteArraySuffix(builder);
    }

    /// <inheritdoc />
    public override ITypeDefinition MakeNullable(bool nullable = true)
    {
        return new TypeDefinition(TypeDefinitionEnum, Namespace, Name, ArrayRanks, AnnotationsWithOuterAnnotation(nullable), ContainingType);
    }

    /// <remarks>
    /// <para>
    /// The new array level goes on the outside and is not annotated, so an annotation already on
    /// the type stays with the element it was asked for: <c>string?</c> made into an array is
    /// <c>string?[]</c>, not <c>string[]?</c>. That is what
    /// <c>TypeDefinitionTests.ArrayRankTests.NullableGoesAfterTheShape</c> pins, and it is version
    /// 1's reading reversed - version 1 moved the annotation onto the array it had just built.
    /// </para>
    /// <para>
    /// The nullable array is reached by annotating afterwards, <c>MakeArray().MakeNullable()</c>,
    /// and the array of nullable elements has a name of its own,
    /// <see cref="ITypeDefinitionExtensions.MakeArrayOfNullable"/>. The two are different types and
    /// neither produces a diagnostic, so prefer the named call to the composition.
    /// </para>
    /// </remarks>
    public override ITypeDefinition MakeArray(int rank)
    {
        return new TypeDefinition(TypeDefinitionEnum, Namespace, Name, ArrayRanksWithOuterRank(rank), AnnotationsWithOuterLevel(), ContainingType);
    }

    /// <summary>
    /// Always empty: this class models a type with no type arguments.
    /// <see cref="GenericTypeDefinition"/> is the one that carries them.
    /// </summary>
    public override IReadOnlyList<ITypeDefinition> TypeArguments => Array.Empty<ITypeDefinition>();

    /// <summary>
    /// Orders by the same identity equality uses, so a sorted collection of type references is
    /// stable across runs - which is what keeps a generated using block byte-identical between
    /// builds.
    /// </summary>
    public override int CompareTo(ITypeDefinition other)
    {
        return TypeDefinitionIdentity.KeyCompare(TypeKey, other);
    }

    /// <summary>
    /// The 1.x form, unchanged: namespace and name, with nothing else in it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>It is not the C# name.</strong> It says <c>System.Void</c> where C# says
    /// <c>void</c>, it says the same thing for <c>int</c> and <c>int[]</c>, and a type with no
    /// namespace keeps its leading dot. It is an identity string that consumers read and assert on,
    /// so it keeps its 1.x shape.
    /// </para>
    /// <example>
    /// <code>
    /// TypeDefinition.Get(typeof(int)).ToString()            // ".int"
    /// TypeDefinition.Get(typeof(int[])).ToString()          // ".int"          - the array is gone
    /// TypeDefinition.Get(typeof(void)).ToString()           // "System.Void"
    /// TypeDefinition.GetNested(outer, "Inner").ToString()   // "Sample.Inner"  - the container is gone
    /// </code>
    /// </example>
    /// <para>
    /// For the C# name use <see cref="ITypeDefinitionExtensions.GetShortName"/>, or
    /// <see cref="WriteTypeName"/> directly. For a name going into generated code, write the type
    /// into an <see cref="IOutputContext"/> instead, so the file qualifies it and counts its
    /// namespace. Hashing and equality use a fuller key than this, so two types this cannot tell
    /// apart are still unequal.
    /// </para>
    /// </remarks>
    public override string ToString()
    {
        // The leading dot on a type with no namespace - `.int` - is part of that shape, and
        // V1CallShapeTests.ToStringKeepsItsV1Shape pins it inside a generic argument because
        // Hardened builds a cache key out of it. That leaves adversary #70 open; the hash it
        // complains about no longer uses this string. See docs/migration-v1-v2.md.
        return $"{Namespace}.{Name}";
    }

    /// <summary>
    /// <c>IOptions&lt;T&gt;</c>, named without a reference to Microsoft.Extensions.Options.
    /// </summary>
    /// <remarks>
    /// The one shortcut here for a type this library does not reference. It is named by namespace
    /// and name rather than by <c>typeof</c>, so a generator emitting options-pattern registrations
    /// does not have to take the package as a dependency to spell the type.
    /// </remarks>
    public static ITypeDefinition IOptions(object typeObject)
    {
        var types = new List<ITypeDefinition>();

        if (typeObject is Type type)
        {
            types.Add(TypeDefinition.Get(type));
        }
        else if (typeObject is ITypeDefinition typeDefinition)
        {
            types.Add(typeDefinition);
        }

        return new GenericTypeDefinition(TypeDefinitionEnum.InterfaceDefinition, "Microsoft.Extensions.Options", "IOptions", types);
    }

    /// <summary>
    /// <c>Task&lt;T&gt;</c>, for the return type of a generated async method.
    /// </summary>
    /// <remarks>
    /// Takes a <see cref="Type"/> or an <see cref="ITypeDefinition"/>, so it composes with a type
    /// this generator is emitting. For a non-generic <c>Task</c> use
    /// <c>TypeDefinition.Get(typeof(Task))</c> - this one always closes over an argument.
    /// </remarks>
    public static ITypeDefinition Task(object typeObject)
    {
        var types = new List<ITypeDefinition>();

        if (typeObject is Type type)
        {
            types.Add(TypeDefinition.Get(type));
        }
        else if (typeObject is ITypeDefinition typeDefinition)
        {
            types.Add(typeDefinition);
        }

        return new GenericTypeDefinition(typeof(Task<>), types);
    }

    /// <summary>
    /// <c>IEnumerable&lt;T&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Takes a <see cref="Type"/> or an <see cref="ITypeDefinition"/>, which is what
    /// <c>TypeDefinition.Get(typeof(IEnumerable&lt;&gt;))</c> cannot do: a closed generic over a type
    /// that does not exist yet has no <see cref="Type"/> to name it with.
    /// </remarks>
    public static ITypeDefinition IEnumerable(object typeObject)
    {
        var types = new List<ITypeDefinition>();

        if (typeObject is Type type)
        {
            types.Add(TypeDefinition.Get(type));
        }
        else if (typeObject is ITypeDefinition typeDefinition)
        {
            types.Add(typeDefinition);
        }

        return new GenericTypeDefinition(typeof(IEnumerable<>), types);
    }

    /// <summary>
    /// <c>List&lt;T&gt;</c>. Takes a <see cref="Type"/> or an <see cref="ITypeDefinition"/>.
    /// </summary>
    public static ITypeDefinition List(object typeObject)
    {
        var types = new List<ITypeDefinition>();

        if (typeObject is Type type)
        {
            types.Add(TypeDefinition.Get(type));
        }
        else if (typeObject is ITypeDefinition typeDefinition)
        {
            types.Add(typeDefinition);
        }

        return new GenericTypeDefinition(typeof(List<>), types);
    }

    /// <summary>
    /// <c>Action&lt;T&gt;</c> over the given arguments - the usual handler type for
    /// <see cref="ClassDefinition.AddEvent(ITypeDefinition, string)"/>.
    /// </summary>
    /// <remarks>
    /// Each argument is a <see cref="Type"/> or an <see cref="ITypeDefinition"/>; anything else is
    /// skipped rather than reported. Passing none gives <c>Action</c> with an empty argument list,
    /// which is not the non-generic <c>Action</c> - use <c>TypeDefinition.Get(typeof(Action))</c>
    /// for that.
    /// </remarks>
    public static ITypeDefinition Action(params object[] typeArguments)
    {
        var types = new List<ITypeDefinition>();

        foreach (var typeObject in typeArguments)
        {
            if (typeObject is Type type)
            {
                types.Add(TypeDefinition.Get(type));
            }
            else if (typeObject is ITypeDefinition typeDefinition)
            {
                types.Add(typeDefinition);
            }
        }

        return new GenericTypeDefinition(typeof(Action<>), types);
    }

    /// <summary>
    /// <c>Func&lt;T, TResult&gt;</c> over the given arguments, result last, the way C# writes it.
    /// </summary>
    /// <remarks>
    /// Each argument is a <see cref="Type"/> or an <see cref="ITypeDefinition"/>; anything else is
    /// skipped rather than reported.
    /// </remarks>
    public static ITypeDefinition Func(params object[] typeArguments)
    {
        var types = new List<ITypeDefinition>();

        foreach (var typeObject in typeArguments)
        {
            if (typeObject is Type type)
            {
                types.Add(TypeDefinition.Get(type));
            }
            else if (typeObject is ITypeDefinition typeDefinition)
            {
                types.Add(typeDefinition);
            }
        }

        return new GenericTypeDefinition(typeof(Func<>), types);
    }

    /// <summary>
    /// A type named by namespace and name, for one that does not exist as a <see cref="Type"/> -
    /// most often a type this generator is also emitting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing checks that the type exists, which is the point: a generator refers to what it is
    /// about to write. Assumed to be a class; use the overload taking a
    /// <see cref="TypeDefinitionEnum"/> where the kind matters.
    /// </para>
    /// <example>
    /// <code>
    /// var result = TypeDefinition.Get("Sample.Models", "Result");
    /// greeter.AddProperty(result, "A");
    /// // ShortName: public Result A { get; set; }   + using Sample.Models;
    /// // Global:    public global::Sample.Models.Result A { get; set; }
    /// </code>
    /// </example>
    /// <para>
    /// <paramref name="isArray"/> and <paramref name="isNullable"/> are the 1.x single flags.
    /// Prefer building the shape by asking for it - <see cref="ITypeDefinition.MakeArray()"/>,
    /// <see cref="ITypeDefinition.MakeNullable"/>,
    /// <see cref="ITypeDefinitionExtensions.MakeArrayOfNullable"/> - which can say things a pair of
    /// flags cannot: <c>int[][]</c>, <c>int[,]</c>, and the difference between <c>int?[]</c> and
    /// <c>int[]?</c>.
    /// </para>
    /// </remarks>
    public static TypeDefinition Get(string ns, string name, bool isArray = false, bool isNullable = false)
    {
        return new TypeDefinition(TypeDefinitionEnum.ClassDefinition, ns, name, isArray, isNullable);
    }

    /// <inheritdoc cref="Get(string, string, bool, bool)" />
    /// <remarks>
    /// The overload that says what kind of type it is. It does not change how the name is written -
    /// it is carried for callers that branch on
    /// <see cref="ITypeDefinition.TypeDefinitionEnum"/>.
    /// </remarks>
    public static TypeDefinition Get(TypeDefinitionEnum definitionEnum,string ns, string name, bool isArray = false, bool isNullable = false)
    {
        return new TypeDefinition(definitionEnum, ns, name, isArray, isNullable);
    }

    /// <summary>
    /// A type with array specifiers, outermost first (<c>[2, 1]</c> is <c>Name[,][]</c>), and
    /// optionally the type it is declared inside.
    /// </summary>
    public static TypeDefinition Get(TypeDefinitionEnum definitionEnum, string ns, string name, IReadOnlyList<int>? arrayRanks, bool isNullable = false, ITypeDefinition? containingType = null)
    {
        return new TypeDefinition(definitionEnum, ns, name, arrayRanks, isNullable, containingType);
    }

    /// <summary>
    /// A type with array specifiers and an annotation for each level, outermost first, then one for
    /// the element - <c>[1]</c> with <c>[false, true]</c> is <c>Name?[]</c>.
    /// </summary>
    public static TypeDefinition Get(TypeDefinitionEnum definitionEnum, string ns, string name, IReadOnlyList<int>? arrayRanks, IReadOnlyList<bool>? nullableAnnotations, ITypeDefinition? containingType)
    {
        return new TypeDefinition(definitionEnum, ns, name, arrayRanks, nullableAnnotations, containingType);
    }

    /// <summary>
    /// A type declared inside <paramref name="containingType"/>, which is what makes it write as
    /// <c>Outer.Inner</c> rather than as a bare <c>Inner</c> that names something else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For a nested type this generator is emitting -
    /// <see cref="ClassDefinition.AddClass"/> declares the nesting, and this is how anything else
    /// refers to it. Building it with <see cref="Get(string, string, bool, bool)"/> and the
    /// container's namespace instead gives a name that resolves to a different type or to nothing.
    /// </para>
    /// <para>
    /// The namespace comes from the container, so it does not need giving. The container is held as
    /// a type rather than as a string, so a generic one keeps its arguments unrendered and
    /// <c>Outer&lt;T&gt;.Inner</c> qualifies the same way in every
    /// <see cref="TypeOutputMode"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="containingType"/> is null.</exception>
    public static TypeDefinition GetNested(ITypeDefinition containingType, string name, TypeDefinitionEnum definitionEnum = TypeDefinitionEnum.ClassDefinition)
    {
        if (containingType == null)
        {
            throw new ArgumentNullException(nameof(containingType));
        }

        return new TypeDefinition(definitionEnum, containingType.Namespace, name, null, false, containingType);
    }

    /// <summary>
    /// The definition for a type that exists: <c>TypeDefinition.Get(typeof(List&lt;string&gt;))</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one to reach for whenever the generator can name the type at compile time. Everything
    /// reflection knows is carried across, in the form the emitter needs rather than the form
    /// reflection reports it in:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// A predefined type becomes its C# keyword - <c>int</c>, not <c>Int32</c> - which carries no
    /// namespace, so it imports nothing and reads the same in every
    /// <see cref="TypeOutputMode"/>.
    /// </description></item>
    /// <item><description>
    /// An array keeps its shape, outermost specifier first, which is the order C# writes them and
    /// the reverse of the order <c>Type.Name</c> gives: <c>typeof(int[,][])</c> becomes
    /// <c>int[,][]</c>, not <c>Int32[][,]</c>.
    /// </description></item>
    /// <item><description>
    /// A closed generic keeps its arguments, each converted the same way; a nested type keeps the
    /// container it is declared in, closed over its own share of the arguments, so
    /// <c>Outer&lt;int&gt;.Inner&lt;string&gt;</c> comes back as itself.
    /// </description></item>
    /// <item><description>
    /// A type parameter becomes a <see cref="TypeParameterDefinition"/> named after it.
    /// </description></item>
    /// </list>
    /// <para>
    /// Use <see cref="Get(string, string, bool, bool)"/> for a type that does not exist yet, which
    /// is the usual case for one this generator is emitting, and
    /// <see cref="GetNested"/> for one declared inside another type it is emitting.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is null.</exception>
    public static ITypeDefinition Get(Type type)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        if (type.IsArray)
        {
            return GetArray(type);
        }

        if (type.IsGenericParameter)
        {
            return new TypeParameterDefinition(type.Name);
        }

        if (IsKnownType(type, out var knownDefinition))
        {
            return knownDefinition!;
        }

        var typeDefinition = TypeDefinitionEnum.ClassDefinition;

        if (type.IsEnum)
        {
            typeDefinition = TypeDefinitionEnum.EnumDefinition;
        }
        else if (type.IsInterface)
        {
            typeDefinition = TypeDefinitionEnum.InterfaceDefinition;
        }

        var containingType = GetContainingType(type);

        if (type.IsConstructedGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();

            var className = genericTypeDefinition.GetGenericName();

            var closingTypes = new List<ITypeDefinition>();

            foreach (var genericArgument in OwnGenericArguments(type))
            {
                closingTypes.Add(Get(genericArgument));
            }

            if (closingTypes.Count == 0)
            {
                // A type with no type parameters of its own, nested in a generic one: the arguments
                // all belong to the container, and writing them here would invent a Inner<T>.
                return new TypeDefinition(typeDefinition, genericTypeDefinition.Namespace ?? "", className, null, false, containingType);
            }

            return new GenericTypeDefinition(typeDefinition,
                genericTypeDefinition.Namespace!, className, closingTypes, null, false, containingType);
        }

        return new TypeDefinition(typeDefinition, type.Namespace ?? "", type.Name, null, false, containingType);
    }

    /// <summary>
    /// The type a nested type is declared in, closed over the arguments that belong to it.
    /// </summary>
    /// <remarks>
    /// Reflection reports the container of a constructed nested type as the *open* generic -
    /// <c>Outer&lt;T&gt;</c>, not <c>Outer&lt;int&gt;</c> - and hangs every argument, the container's
    /// included, off the nested type. Closing the container back over its own share is what turns
    /// <c>Outer&lt;int&gt;.Inner&lt;string&gt;</c> back into itself.
    /// </remarks>
    private static ITypeDefinition? GetContainingType(Type type)
    {
        var declaringType = type.DeclaringType;

        if (declaringType == null)
        {
            return null;
        }

        if (declaringType.IsGenericTypeDefinition && type.IsConstructedGenericType)
        {
            var declaringArity = declaringType.GetGenericArguments().Length;
            var arguments = type.GetGenericArguments();

            if (declaringArity > 0 && declaringArity <= arguments.Length)
            {
                var containerArguments = new Type[declaringArity];

                Array.Copy(arguments, containerArguments, declaringArity);

                declaringType = declaringType.MakeGenericType(containerArguments);
            }
        }

        return Get(declaringType);
    }

    /// <summary>
    /// The generic arguments the type declares itself, without the ones inherited from its container.
    /// </summary>
    private static IReadOnlyList<Type> OwnGenericArguments(Type type)
    {
        var arguments = type.GetGenericArguments();

        var declaringType = type.DeclaringType;

        if (declaringType is not { IsGenericType: true })
        {
            return arguments;
        }

        var inherited = declaringType.GetGenericArguments().Length;

        if (inherited <= 0 || inherited > arguments.Length)
        {
            return arguments;
        }

        var own = new Type[arguments.Length - inherited];

        Array.Copy(arguments, inherited, own, 0, own.Length);

        return own;
    }

    /// <summary>
    /// Unwraps an array type one array at a time, outermost first, which is the order C# writes the
    /// specifiers in. Reflection writes them the other way round - <c>typeof(int[,][])</c> is named
    /// <c>Int32[][,]</c> - so reading <c>Type.Name</c> gives a reversed, and previously
    /// doubled, answer.
    /// </summary>
    private static ITypeDefinition GetArray(Type type)
    {
        var ranks = new List<int>();
        var elementType = type;

        while (elementType.IsArray)
        {
            ranks.Add(elementType.GetArrayRank());

            elementType = elementType.GetElementType()!;
        }

        var definition = Get(elementType);

        for (var i = ranks.Count - 1; i >= 0; i--)
        {
            definition = definition.MakeArray(ranks[i]);
        }

        return definition;
    }

    /// <summary>
    /// The C# keyword for every predefined type, so a type reaches output as the compiler spells it
    /// rather than as reflection names it.
    /// </summary>
    /// <remarks>
    /// A keyword carries no namespace, so writing one imports nothing and it reads the same in every
    /// <see cref="TypeOutputMode"/>. <c>nint</c> and <c>nuint</c> are the same runtime types as
    /// <see cref="IntPtr"/> and <see cref="UIntPtr"/> and cannot be told apart by reflection; they are
    /// written as the keywords, which requires C# 9 in the consuming code.
    /// </remarks>
    private static readonly Dictionary<Type, ITypeDefinition> _knownTypes = new()
    {
        { typeof(object), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "object", false) },
        { typeof(ulong), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "ulong", false) },
        { typeof(long), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "long", false) },
        { typeof(uint), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "uint", false) },
        { typeof(string), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "string", false) },
        { typeof(int), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "int", false) },
        { typeof(short), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "short", false) },
        { typeof(ushort), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "ushort", false) },
        { typeof(byte), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "byte", false) },
        { typeof(sbyte), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "sbyte", false) },
        { typeof(char), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "char", false) },
        { typeof(float), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "float", false) },
        { typeof(double), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "double", false) },
        { typeof(decimal), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "decimal", false) },
        { typeof(bool), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "bool", false) },
        { typeof(IntPtr), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "nint", false) },
        { typeof(UIntPtr), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "nuint", false) },
    };
    /// <remarks>
    /// Array forms are not listed: an array is unwrapped to its element type first, so
    /// <c>float[][]</c> reaches the keyword the same way <c>float</c> does, and no table has to
    /// enumerate the shapes.
    /// </remarks>
    private static bool IsKnownType(Type type, out ITypeDefinition? typeDefinition)
    {
        return _knownTypes.TryGetValue(type, out typeDefinition);
    }
}