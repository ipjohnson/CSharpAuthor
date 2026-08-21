using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CSharpAuthor;

public class TypeDefinition : BaseTypeDefinition
{
    private int? _hashCode;

    public TypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string ns, string name, bool isArray, bool isNullable = false) : base(typeDefinitionEnum, ns, name,  isArray, isNullable)
    {
            
    }
        
    public override IEnumerable<string> KnownNamespaces
    {
        get { yield return Namespace; }
    }

    public override void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName)
    {
        
        if (Name == "Void" && Namespace == "System")
        {
            builder.Append("void");
            return;
        }

        if (!string.IsNullOrEmpty(Namespace))
        {
            if (typeOutputMode == TypeOutputMode.Global)
            {
                builder.Append("global::");
                builder.Append(Namespace);
                builder.Append('.');

            }
            else if (typeOutputMode == TypeOutputMode.FullName)
            {
                builder.Append(Namespace);
                builder.Append('.');
            }
        }


        builder.Append(Name);
        
        if (IsArray)
        {
            builder.Append("[]");
        }

        if (IsNullable)
        {
            builder.Append("?");
        }
    }

    public override ITypeDefinition MakeNullable(bool nullable = true)
    {
        return new TypeDefinition(TypeDefinitionEnum, Namespace, Name, IsArray, nullable);
    }

    public override ITypeDefinition MakeArray()
    {
        return new TypeDefinition(TypeDefinitionEnum, Namespace, Name, true, IsNullable);
    }

    public override IReadOnlyList<ITypeDefinition> TypeArguments => Array.Empty<ITypeDefinition>();

    public override int CompareTo(ITypeDefinition other)
    {
        return BaseCompareTo(other);
    }

    public override bool Equals(object obj)
    {
        if (obj is TypeDefinition typeDefinition)
        {
            return CompareTo(typeDefinition) == 0;
        }

        return false;
    }

    public override int GetHashCode()
    {
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        return _hashCode ??= ToString().GetHashCode();
    }

    public override string ToString()
    {
        return $"{Namespace}.{Name}";
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

    public static ITypeDefinition Get(Type type)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
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

        if (type.IsConstructedGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();

            var className = genericTypeDefinition.GetGenericName();

            var closingTypes = new List<ITypeDefinition>();

            foreach (var genericArgument in type.GetGenericArguments())
            {
                closingTypes.Add(Get(genericArgument));
            }

            return new GenericTypeDefinition(typeDefinition, 
                genericTypeDefinition.Namespace!, className, closingTypes, type.IsArray);
        }

        return new TypeDefinition(typeDefinition, type.Namespace!, type.Name, type.IsArray);
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
    private static readonly Dictionary<Type, ITypeDefinition> _knownArrayTypes = new()
    {
        { typeof(object[]), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "object", true) },
        { typeof(string[]), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "string", true) },
        { typeof(int[]), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "int", true) },
        { typeof(uint[]), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "uint", true) },
        { typeof(long[]), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "long", true) },
        { typeof(ulong[]), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "ulong", true) },
        { typeof(short[]), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "short", true) },
        { typeof(ushort[]), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "ushort", true) },
        { typeof(byte[]), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "byte", true) },
        { typeof(bool[]), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "bool", true) },
        { typeof(double[]), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "double", true) },
        { typeof(decimal[]), new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", "decimal", true) },
    };

    private static bool IsKnownType(Type type, out ITypeDefinition? typeDefinition)
    {
        if (type.IsArray)
        {
            return _knownArrayTypes.TryGetValue(type, out typeDefinition);
        }

        return _knownTypes.TryGetValue(type, out typeDefinition);
    }
}