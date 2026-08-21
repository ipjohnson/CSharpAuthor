using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// A named thing used as a value - a local, a parameter, a field, a loop variable - and the member
/// accesses and calls reached off it.
/// </summary>
/// <remarks>
/// The alternative to repeating a name as a string in every statement that mentions it. A
/// declaration hands one of these back - <see cref="FieldDefinition.Instance"/>,
/// <see cref="ForEachDefinition.Instance"/>, <see cref="ParameterDefinition"/> itself - so the body
/// and the declaration cannot drift apart.
/// </remarks>
public class InstanceDefinition : BaseOutputComponent
{
    /// <summary>
    /// A value referred to by <paramref name="name"/>.
    /// </summary>
    /// <remarks>
    /// Constructing one by hand is naming something this library did not declare. Prefer the
    /// <c>Instance</c> property of whatever declared it.
    /// </remarks>
    public InstanceDefinition(string name)
    {
        Name = name;
    }
        
    /// <summary>
    /// The name, as given. May be dotted - <see cref="Property"/> builds <c>a.b.c</c> into one
    /// name - and each segment is escaped when it is written.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// A call on this instance: <c>x.Go(1)</c>.
    /// </summary>
    /// <remarks>
    /// <paramref name="parameters"/> are expressions, so a <see cref="string"/> arrives unquoted;
    /// use <see cref="SyntaxHelpers.QuoteString"/> for a literal.
    /// </remarks>
    public InvokeDefinition Invoke(string methodName, params object[] parameters)
    {
        var invokeDefinition = new InvokeDefinition(Name, methodName) { Indented = false };

        foreach (var parameter in parameters)
        {
            invokeDefinition.AddArgument(parameter);
        }

        return invokeDefinition;
    }
        
    /// <summary>
    /// A call with explicit type arguments: <c>x.Go&lt;int&gt;(1)</c>.
    /// </summary>
    /// <remarks>
    /// This is the generic call to use on a named receiver. The extension-method form,
    /// <see cref="SyntaxHelpers.InvokeGeneric(IOutputComponent, string, IReadOnlyList{ITypeDefinition}, object[])"/>,
    /// emits a doubled dot and does not compile.
    /// </remarks>
    public InvokeGenericDefinition InvokeGeneric(string methodName, IEnumerable<ITypeDefinition> genericArgs, params object[] parameters)
    {
        var invokeDefinition = 
            new InvokeGenericDefinition(Name, methodName, genericArgs.ToList()) { Indented = false };

        foreach (var parameter in parameters)
        {
            invokeDefinition.AddArgument(parameter);
        }

        return invokeDefinition;
    }

    /// <summary>
    /// A member reached off this one: <c>x.Name</c>, as another instance that can be walked
    /// further.
    /// </summary>
    /// <remarks>
    /// Chains - <c>instance.Property("A").Property("B")</c> is <c>x.A.B</c> - because what comes
    /// back is an instance rather than a finished expression.
    /// </remarks>
    public InstanceDefinition Property(string propertyName)
    {
        return new InstanceDefinition(Name + "." + propertyName);
    }

    /// <summary>
    /// The instance, by name.
    /// </summary>
    /// <remarks>
    /// Each dotted segment is escaped, because <see cref="Property"/> builds <c>a.b.c</c> into one
    /// name. <c>this</c> and <c>base</c> are left alone - they arrive here as expressions rather
    /// than as names, and <c>@this</c> would not mean the same thing.
    /// </remarks>
    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.Write(CSharpIdentifier.EscapeReference(Name));
    }
}