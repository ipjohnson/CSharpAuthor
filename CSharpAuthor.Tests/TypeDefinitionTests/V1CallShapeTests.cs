using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace CSharpAuthor.Tests.TypeDefinitionTests;

/// <summary>
/// Every way V1 had of building a type still compiles and still means the same thing.
/// </summary>
/// <remarks>
/// The array and container work added constructor and factory overloads beside the existing ones,
/// and an overload added beside one whose extra parameters are all optional is the kind of change
/// that resolves differently without anyone noticing - or stops resolving at all. This file is a
/// compile-time test as much as a runtime one: it calls each V1 shape at each arity the consumers
/// use, so a shift in overload resolution fails the build rather than the generated code.
/// </remarks>
public class V1CallShapeTests
{
    [Fact]
    public void TypeDefinitionGetShapes()
    {
        Assert.Equal("Name", TypeDefinition.Get("Ns", "Name").GetShortName());
        Assert.Equal("Name[]", TypeDefinition.Get("Ns", "Name", true).GetShortName());
        Assert.Equal("Name[]?", TypeDefinition.Get("Ns", "Name", true, true).GetShortName());

        Assert.Equal("Name", TypeDefinition.Get(TypeDefinitionEnum.InterfaceDefinition, "Ns", "Name").GetShortName());
        Assert.Equal("Name[]", TypeDefinition.Get(TypeDefinitionEnum.InterfaceDefinition, "Ns", "Name", true).GetShortName());
        Assert.Equal("Name?", TypeDefinition.Get(TypeDefinitionEnum.EnumDefinition, "Ns", "Name", false, true).GetShortName());

        Assert.Equal(
            TypeDefinitionEnum.InterfaceDefinition,
            TypeDefinition.Get(TypeDefinitionEnum.InterfaceDefinition, "Ns", "Name").TypeDefinitionEnum);
    }

    /// <summary>
    /// The shape a consumer uses to rebuild a type from the parts of another one.
    /// </summary>
    [Fact]
    public void RebuildingATypeFromItsParts()
    {
        var source = TypeDefinition.Get(typeof(Task<string>)).MakeArray();

        var rebuilt = TypeDefinition.Get(source.TypeDefinitionEnum, source.Namespace, source.Name, source.IsArray);

        Assert.Equal("Task[]", rebuilt.GetShortName());
        Assert.True(rebuilt.IsArray);

        // The same shape, keeping the ranks the bool cannot carry.
        var withShape = TypeDefinition.Get(source.TypeDefinitionEnum, source.Namespace, source.Name, source.ArrayRanks);

        Assert.Equal("Task[]", withShape.GetShortName());
    }

    [Fact]
    public void TypeDefinitionConstructorShapes()
    {
        Assert.Equal("Name", new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "Ns", "Name", false).GetShortName());
        Assert.Equal("Name[]?", new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "Ns", "Name", true, true).GetShortName());
    }

    [Fact]
    public void GenericTypeDefinitionConstructorShapes()
    {
        var arguments = new ITypeDefinition[] { TypeDefinition.Get(typeof(string)) };

        Assert.Equal("Func<string>", new GenericTypeDefinition(typeof(Func<>), arguments).GetShortName());
        Assert.Equal("Func<string>[]", new GenericTypeDefinition(typeof(Func<>), arguments, true).GetShortName());
        Assert.Equal("Func<string>?", new GenericTypeDefinition(typeof(Func<>), arguments, false, true).GetShortName());

        Assert.Equal(
            "Holder<string>",
            new GenericTypeDefinition(TypeDefinitionEnum.ClassDefinition, "Ns", "Holder", arguments).GetShortName());

        Assert.Equal(
            "Holder<string>[]",
            new GenericTypeDefinition(TypeDefinitionEnum.ClassDefinition, "Ns", "Holder", arguments, true).GetShortName());

        Assert.Equal(
            "Holder<string>[]?",
            new GenericTypeDefinition(TypeDefinitionEnum.ClassDefinition, "Ns", "Holder", arguments, true, true).GetShortName());
    }

    [Fact]
    public void StaticHelperShapes()
    {
        Assert.Equal("IOptions<string>", TypeDefinition.IOptions(typeof(string)).GetShortName());
        Assert.Equal("Task<string>", TypeDefinition.Task(typeof(string)).GetShortName());
        Assert.Equal("IEnumerable<string>", TypeDefinition.IEnumerable(typeof(string)).GetShortName());
        Assert.Equal("List<string>", TypeDefinition.List(typeof(string)).GetShortName());
        Assert.Equal("Action<string>", TypeDefinition.Action(typeof(string)).GetShortName());
        Assert.Equal("Func<string,int>", TypeDefinition.Func(typeof(string), typeof(int)).GetShortName());

        Assert.Equal("List<string>", TypeDefinition.List(TypeDefinition.Get(typeof(string))).GetShortName());
    }

    [Fact]
    public void MakeOpenTypeStillOpens()
    {
        var closed = new GenericTypeDefinition(
            TypeDefinitionEnum.InterfaceDefinition,
            "Ns",
            "IHolder",
            new ITypeDefinition[] { TypeDefinition.Get(typeof(string)), TypeDefinition.Get(typeof(int)) });

        Assert.Equal("IHolder<,>", closed.MakeOpenType().GetShortName());
    }

    [Fact]
    public void TypeQueriesStillAnswer()
    {
        var type = TypeDefinition.Get(typeof(Task<string>));

        Assert.Equal("Task", type.Name);
        Assert.Equal("System.Threading.Tasks", type.Namespace);
        Assert.False(type.IsArray);
        Assert.False(type.IsNullable);
        Assert.Equal(TypeDefinitionEnum.ClassDefinition, type.TypeDefinitionEnum);
        Assert.Single(type.TypeArguments);
        Assert.Contains("System.Threading.Tasks", type.KnownNamespaces);
        Assert.True(type.MakeNullable().IsNullable);
        Assert.True(type.MakeArray().IsArray);
    }

    /// <summary>
    /// Equality is still by value, and still tells a nullable from a non-nullable and an interface from
    /// a class.
    /// </summary>
    [Fact]
    public void ValueEqualityStillHolds()
    {
        Assert.Equal(TypeDefinition.Get("Ns", "A"), TypeDefinition.Get("Ns", "A"));
        Assert.Equal(TypeDefinition.Get("Ns", "A").GetHashCode(), TypeDefinition.Get("Ns", "A").GetHashCode());

        Assert.NotEqual(TypeDefinition.Get("Ns", "A"), TypeDefinition.Get("Ns", "B"));
        Assert.NotEqual(TypeDefinition.Get("Ns", "A"), TypeDefinition.Get("Other", "A"));
        Assert.NotEqual(TypeDefinition.Get("Ns", "A"), TypeDefinition.Get("Ns", "A", isNullable: true));
        Assert.NotEqual(
            (ITypeDefinition)TypeDefinition.Get(TypeDefinitionEnum.ClassDefinition, "Ns", "A"),
            TypeDefinition.Get(TypeDefinitionEnum.InterfaceDefinition, "Ns", "A"));

        Assert.Equal(
            (ITypeDefinition)TypeDefinition.Get(typeof(Task<string>)),
            TypeDefinition.Get(typeof(Task<string>)));

        var byType = new Dictionary<ITypeDefinition, string>
        {
            { TypeDefinition.Get(typeof(Task<string>)), "task" }
        };

        Assert.Equal("task", byType[TypeDefinition.Get(typeof(Task<string>))]);
    }
}
