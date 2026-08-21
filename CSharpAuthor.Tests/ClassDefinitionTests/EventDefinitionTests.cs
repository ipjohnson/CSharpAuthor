using System;
using Xunit;

namespace CSharpAuthor.Tests.ClassDefinitionTests;

public class EventDefinitionTests
{
    [Fact]
    public void FieldLikeEvent()
    {
        var classDefinition = new ClassDefinition("Publisher");
        classDefinition.AddEvent(typeof(EventHandler), "Changed");

        var context = new OutputContext();
        classDefinition.WriteOutput(context);

        AssertEqual.WithoutNewLine(FieldLikeOutput, context.Output());
    }

    private const string FieldLikeOutput =
        @"public class Publisher
{
    public event EventHandler Changed;
}
";

    [Fact]
    public void EventWithAccessors()
    {
        var classDefinition = new ClassDefinition("Publisher");

        var eventDefinition = classDefinition.AddEvent(typeof(EventHandler), "Changed");
        eventDefinition.Add.AddIndentedStatement("_inner.Changed += value");
        eventDefinition.Remove.AddIndentedStatement("_inner.Changed -= value");

        var context = new OutputContext();
        classDefinition.WriteOutput(context);

        AssertEqual.WithoutNewLine(AccessorsOutput, context.Output());
    }

    private const string AccessorsOutput =
        @"public class Publisher
{
    public event EventHandler Changed
    {
        add
        {
            _inner.Changed += value;
        }
        remove
        {
            _inner.Changed -= value;
        }
    }
}
";
}
