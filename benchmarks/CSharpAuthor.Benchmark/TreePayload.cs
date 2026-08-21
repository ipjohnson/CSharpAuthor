using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CSharpAuthor;

namespace Bench;

/// <summary>
/// The V2-HANDOFF.md §10 payload, built through the CSharpAuthor public API:
/// one class, 25 init-only properties, a constructor assigning all of them, and a
/// method with 27 statements.
/// </summary>
/// <remarks>
/// <para>
/// This file is the *only* definition of the payload and it is always taken from the
/// harness's own checkout, never from the library checkout being measured - that is what
/// makes the V1 and V2 numbers comparable.
/// </para>
/// <para>
/// The <see cref="ITypeDefinition"/> instances are hoisted to statics on purpose. Real
/// generators hold their types in a static <c>KnownTypes</c> holder and build many files
/// from them; putting <c>TypeDefinition.Get(typeof(T))</c> inside the timed region would
/// measure <see cref="System.Type"/> reflection, which is identical in V1 and V2 and is
/// not what the gate is about. Everything else - tree construction and serialisation - is
/// inside the timed region.
/// </para>
/// </remarks>
internal static class TreePayload
{
    private static readonly ITypeDefinition StringType = TypeDefinition.Get(typeof(string));
    private static readonly ITypeDefinition IntType = TypeDefinition.Get(typeof(int));
    private static readonly ITypeDefinition BoolType = TypeDefinition.Get(typeof(bool));
    private static readonly ITypeDefinition GuidType = TypeDefinition.Get(typeof(Guid));
    private static readonly ITypeDefinition DateTimeType = TypeDefinition.Get(typeof(DateTime));
    private static readonly ITypeDefinition DecimalType = TypeDefinition.Get(typeof(decimal));
    private static readonly ITypeDefinition DoubleType = TypeDefinition.Get(typeof(double));
    private static readonly ITypeDefinition LongType = TypeDefinition.Get(typeof(long));
    private static readonly ITypeDefinition TimeSpanType = TypeDefinition.Get(typeof(TimeSpan));
    private static readonly ITypeDefinition TagListType = TypeDefinition.Get(typeof(IReadOnlyList<string>));
    private static readonly ITypeDefinition CounterMapType = TypeDefinition.Get(typeof(IReadOnlyDictionary<string, int>));

    private static readonly ITypeDefinition StringBuilderType = TypeDefinition.Get(typeof(StringBuilder));
    private static readonly ITypeDefinition CultureInfoType = TypeDefinition.Get(typeof(CultureInfo));
    private static readonly ITypeDefinition ExceptionType = TypeDefinition.Get(typeof(Exception));

    /// <summary>The 25 init-only properties, and the constructor parameter each is assigned from.</summary>
    internal static readonly PropertySpec[] Properties =
    {
        new(StringType, "Id", "id"),
        new(StringType, "Name", "name"),
        new(StringType, "Category", "category"),
        new(StringType, "Description", "description"),
        new(StringType, "ScopeName", "scopeName"),
        new(IntType, "Order", "order"),
        new(IntType, "Version", "version"),
        new(IntType, "RetryLimit", "retryLimit"),
        new(BoolType, "IsEnabled", "isEnabled"),
        new(BoolType, "IsTransient", "isTransient"),
        new(BoolType, "AllowsNull", "allowsNull"),
        new(GuidType, "Key", "key"),
        new(GuidType, "CorrelationId", "correlationId"),
        new(DateTimeType, "CreatedAt", "createdAt"),
        new(DateTimeType, "ModifiedAt", "modifiedAt"),
        new(DecimalType, "Amount", "amount"),
        new(DecimalType, "Discount", "discount"),
        new(DoubleType, "Ratio", "ratio"),
        new(DoubleType, "Weight", "weight"),
        new(LongType, "Ticks", "ticks"),
        new(LongType, "Sequence", "sequence"),
        new(TimeSpanType, "Duration", "duration"),
        new(TimeSpanType, "Timeout", "timeout"),
        new(TagListType, "Tags", "tags"),
        new(CounterMapType, "Counters", "counters"),
    };

    internal readonly struct PropertySpec
    {
        public PropertySpec(ITypeDefinition type, string name, string parameter)
        {
            Type = type;
            Name = name;
            Parameter = parameter;
        }

        public ITypeDefinition Type { get; }

        public string Name { get; }

        public string Parameter { get; }
    }

    /// <summary>Builds the payload tree and serialises it. One call == one generated file.</summary>
    public static string Generate()
    {
        var file = new CSharpFileDefinition("CSharpAuthor.Benchmark.Generated");

        var classDefinition = file.AddClass("BenchmarkPayload");

        foreach (var property in Properties)
        {
            classDefinition.AddProperty(property.Type, property.Name).Set!.IsInit = true;
        }

        var constructor = classDefinition.AddConstructor();

        foreach (var property in Properties)
        {
            constructor.AddParameter(property.Type, property.Parameter);
        }

        foreach (var property in Properties)
        {
            constructor.Assign(property.Parameter).To(property.Name);
        }

        AddExecuteMethod(classDefinition);

        var outputContext = new OutputContext();

        file.WriteOutput(outputContext);

        return outputContext.Output();
    }

    /// <summary>The 27-statement method. Counted at the top level of the body; 5 of the 27 open a nested block.</summary>
    private static void AddExecuteMethod(ClassDefinition classDefinition)
    {
        var method = classDefinition.AddMethod("Execute");

        method.SetReturnType(StringType);
        method.AddParameter(IntType, "retryCount");
        method.AddParameter(BoolType, "verbose");

        // 1 - 6: locals.
        var builder = method.Assign(SyntaxHelpers.New(StringBuilderType)).ToVar("builder");
        method.Assign(Inline(SyntaxHelpers.Property(DateTimeType, "UtcNow"))).ToVar("timestamp");
        method.Assign("0").ToVar("attempts");
        method.Assign("false").ToVar("completed");
        method.Assign(new InstanceDefinition("Key").Invoke("ToString")).ToVar("identifier");
        method.Assign(SyntaxHelpers.QuoteString(";")).ToVar("separator");

        // 7 - 12: appends.
        method.AddIndentedStatement(builder.Invoke("Append", SyntaxHelpers.QuoteString("Id=")));
        method.AddIndentedStatement(builder.Invoke("Append", "Id"));
        method.AddIndentedStatement(builder.Invoke("Append", "separator"));
        method.AddIndentedStatement(builder.Invoke("Append", SyntaxHelpers.QuoteString("Name=")));
        method.AddIndentedStatement(builder.Invoke("Append", "Name"));
        method.AddIndentedStatement(builder.Invoke("Append", "separator"));

        // 13: if / else.
        var ifBlock = method.If(SyntaxHelpers.And("IsEnabled", "verbose"));
        ifBlock.AddIndentedStatement(builder.Invoke("Append", SyntaxHelpers.QuoteString("enabled")));
        var elseBlock = ifBlock.Else();
        elseBlock.AddIndentedStatement(builder.Invoke("Append", SyntaxHelpers.QuoteString("disabled")));

        // 14: foreach.
        var forEachBlock = method.ForEach("tag", new InstanceDefinition("Tags"));
        forEachBlock.AddIndentedStatement(builder.Invoke("Append", "tag"));
        forEachBlock.AddIndentedStatement(builder.Invoke("Append", "separator"));

        // 15: while.
        var whileTest = SyntaxHelpers.LessThan("attempts", "retryCount");
        whileTest.PrintParentheses = false;
        var whileBlock = method.While(whileTest);
        whileBlock.Assign(SyntaxHelpers.Add("attempts", "1")).To("attempts");

        // 16 - 18: derived locals.
        method.Assign(SyntaxHelpers.Multiply("Order", "Version")).ToVar("total");
        method.Assign(new InstanceDefinition("Ratio").Invoke(
            "ToString", Inline(SyntaxHelpers.Property(CultureInfoType, "InvariantCulture")))).ToVar("ratioText");
        method.Assign(new InstanceDefinition("Amount").Invoke(
            "ToString", Inline(SyntaxHelpers.Property(CultureInfoType, "InvariantCulture")))).ToVar("amountText");

        // 19 - 20: appends.
        method.AddIndentedStatement(builder.Invoke("Append", "ratioText"));
        method.AddIndentedStatement(builder.Invoke("Append", "amountText"));

        // 21: try / catch.
        var tryBlock = method.Try();
        tryBlock.AddIndentedStatement(builder.Invoke("Append", new InstanceDefinition("Counters.Count")));
        var catchBlock = tryBlock.Catch(ExceptionType, "exception");
        catchBlock.AddIndentedStatement(builder.Invoke("Append", new InstanceDefinition("exception.Message")));

        // 22: if.
        var completedBlock = method.If(SyntaxHelpers.GreaterThan("total", "100"));
        completedBlock.Assign("true").To("completed");

        // 23 - 26: appends.
        method.AddIndentedStatement(builder.Invoke(
            "Append", new InstanceDefinition("timestamp").Invoke("ToString", SyntaxHelpers.QuoteString("O"))));
        method.AddIndentedStatement(builder.Invoke("Append", "identifier"));
        method.AddIndentedStatement(builder.Invoke("Append", "completed"));
        method.AddIndentedStatement(builder.Invoke(
            "Append", SyntaxHelpers.NullCoalesce("Description", SyntaxHelpers.QuoteString("none"))));

        // 27: return.
        method.Return(builder.Invoke("ToString"));
    }

    private static T Inline<T>(T component) where T : BaseOutputComponent
    {
        component.Indented = false;

        return component;
    }
}
