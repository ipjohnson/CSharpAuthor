using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CSharpAuthor;

public class TypeDefinition : BaseTypeDefinition
{
    private int? _hashCode;

    public TypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string ns, string name, bool isArray, bool isNullable = false, ITypeDefinition? containingType = null)
        : base(typeDefinitionEnum, ns, name,  isArray, isNullable, containingType)
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

        WriteQualification(builder, typeOutputMode);

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
        return new TypeDefinition(TypeDefinitionEnum, Namespace, Name, IsArray, nullable, ContainingType);
    }

    /// <inheritdoc cref="ArrayTypeDefinition.MakeArray"/>
    public override ITypeDefinition MakeArray()
    {
        return new ArrayTypeDefinition(this);
    }

    public override IReadOnlyList<ITypeDefinition> TypeArguments => Array.Empty<ITypeDefinition>();

    public override int CompareTo(ITypeDefinition? other)
    {
        return BaseCompareTo(other);
    }

    public override bool Equals(object? obj)
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

    /// <summary>
    /// A nested type, written with the container it is declared in.
    /// </summary>
    public static TypeDefinition GetNested(ITypeDefinition containingType, string name, TypeDefinitionEnum definitionEnum = TypeDefinitionEnum.ClassDefinition)
    {
        return new TypeDefinition(
            definitionEnum, containingType.Namespace, name, false, false, containingType);
    }

    public static ITypeDefinition Get(Type type)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        // An array is its element plus a rank, recursively - so int[][] is an array of int[]
        // rather than an int carrying two flags it has nowhere to put.
        if (type.IsArray)
        {
            return new ArrayTypeDefinition(Get(type.GetElementType()!), type.GetArrayRank());
        }

        // int? arrives as Nullable<int>. Rendering it as a constructed generic is correct C# and
        // is not what anyone writes.
        var underlyingType = Nullable.GetUnderlyingType(type);

        if (underlyingType != null)
        {
            return Get(underlyingType).MakeNullable();
        }

        var specialType = SpecialTypes.Get(type);

        if (specialType != null)
        {
            return specialType;
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

        // A nested type's own Name is just Inner, so without its container it binds to whatever
        // Inner is in scope where it is used.
        var containingType = type.IsNested && type.DeclaringType != null
            ? Get(type.DeclaringType)
            : null;

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
                genericTypeDefinition.Namespace!, className, closingTypes, false, false, containingType);
        }

        return new TypeDefinition(typeDefinition, type.Namespace ?? "", type.Name, false, false, containingType);
    }

}