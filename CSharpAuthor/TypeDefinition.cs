using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CSharpAuthor;

public class TypeDefinition : BaseTypeDefinition
{
    public TypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string ns, string name, bool isArray, bool isNullable = false) : base(typeDefinitionEnum, ns, name,  isArray, isNullable)
    {

    }

    public TypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string ns, string name, IReadOnlyList<int>? arrayRanks, bool isNullable = false, ITypeDefinition? containingType = null)
        : base(typeDefinitionEnum, ns, name, arrayRanks, isNullable, containingType)
    {

    }

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

    public override void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName)
    {

        if (Name == "Void" && Namespace == "System")
        {
            builder.Append("void");
            return;
        }

        WriteQualifier(builder, typeOutputMode);

        builder.Append(Name);

        WriteArrayRanks(builder);

        if (IsNullable)
        {
            builder.Append("?");
        }
    }

    public override ITypeDefinition MakeNullable(bool nullable = true)
    {
        return new TypeDefinition(TypeDefinitionEnum, Namespace, Name, ArrayRanks, nullable, ContainingType);
    }

    public override ITypeDefinition MakeArray(int rank)
    {
        return new TypeDefinition(TypeDefinitionEnum, Namespace, Name, ArrayRanksWithOuterRank(rank), IsNullable, ContainingType);
    }

    public override IReadOnlyList<ITypeDefinition> TypeArguments => Array.Empty<ITypeDefinition>();

    public override int CompareTo(ITypeDefinition other)
    {
        return BaseCompareTo(other);
    }

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

    public static TypeDefinition Get(string ns, string name, bool isArray = false, bool isNullable = false)
    {
        return new TypeDefinition(TypeDefinitionEnum.ClassDefinition, ns, name, isArray, isNullable);
    }
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
    /// A type declared inside <paramref name="containingType"/>, which is what makes it write as
    /// <c>Outer.Inner</c> rather than as a bare <c>Inner</c> that names something else.
    /// </summary>
    public static TypeDefinition GetNested(ITypeDefinition containingType, string name, TypeDefinitionEnum definitionEnum = TypeDefinitionEnum.ClassDefinition)
    {
        if (containingType == null)
        {
            throw new ArgumentNullException(nameof(containingType));
        }

        return new TypeDefinition(definitionEnum, containingType.Namespace, name, null, false, containingType);
    }

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
    /// <c>Int32[][,]</c> - so reading <see cref="Type.Name"/> gives a reversed, and previously
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