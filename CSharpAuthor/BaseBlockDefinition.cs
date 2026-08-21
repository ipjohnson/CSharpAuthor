using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CSharpAuthor;

public abstract class BaseBlockDefinition : BaseOutputComponent
{
    protected readonly List<IOutputComponent> StatementList = new ();

    public int StatementCount => StatementList.Count;

    public T Add<T>(T component) where T : IOutputComponent
    {
        StatementList.Add(component);

        return component;
    }

    public virtual object AddIndentedStatement(object component)
    {
        StatementList.Add(new IndentedStatementComponent(CodeOutputComponent.Get( component)));

        return component;
    }

    /// <summary>
    /// A statement with types substituted into it, held as pieces so the types are still types when
    /// the file is serialized.
    /// </summary>
    /// <remarks>
    /// A <c>{argN}</c> used to be replaced with the type's short name on the spot, which fixed the
    /// text before anything knew what mode the file would be written in or what else it would
    /// contain. The name went in unqualified even in a file that qualified everything, and the
    /// namespace was declared on the side to make it resolve. The pieces keep the type instead:
    /// it is rendered with everything else, at the end.
    ///
    /// A <c>[argN]</c> is still substituted here, because it is text by definition.
    /// </remarks>
    public virtual CodeOutputComponent AddCode(string statement, params object[] types)
    {
        var parts = new List<object> { statement };

        if (types is { Length: > 0 })
        {
            for (var index = 0; index < types.Length; index++)
            {
                var value = types[index];
                var typeSwapString =
                    "{arg" + (index + 1).ToString(CultureInfo.InvariantCulture) + "}";

                if (PartsContain(parts, typeSwapString))
                {
                    if (value is Type typeValue)
                    {
                        value = TypeDefinition.Get(typeValue);
                    }

                    ReplaceInParts(parts, typeSwapString, GetSubstitutionParts(value));
                }
                else
                {
                    var rawSwapString =
                        "[arg" + (index + 1).ToString(CultureInfo.InvariantCulture) + "]";

                    if (PartsContain(parts, rawSwapString))
                    {
                        ReplaceInParts(
                            parts, rawSwapString, new object[] { LiteralFormatter.Format(value) });
                    }
                }
            }
        }

        return Add(CodeOutputComponent.FromParts(parts));
    }

    private static bool PartsContain(List<object> parts, string marker)
    {
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i] is string text && text.IndexOf(marker, StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The pieces a <c>{argN}</c> substitution becomes - more than one where the value is written
    /// as a type plus text.
    /// </summary>
    /// <remarks>
    /// An enum value is the case that needs two: it is written as <c>Type.Member</c>, and the
    /// <c>Type</c> half has to stay a type so the file derives its namespace. Handed over as text it
    /// was the member name alone - <c>var x = Singleton;</c>, CS0103 - which is the section 1
    /// defect reached through <c>AddCode</c> rather than through a raw string.
    /// </remarks>
    private IReadOnlyList<object> GetSubstitutionParts(object value)
    {
        if (value is ITypeDefinition typeDefinition)
        {
            return new object[] { typeDefinition };
        }

        if (value is Enum && CodeOutputComponent.Get(value) is CodeOutputComponent component)
        {
            return component.Parts ?? new object[] { GetObjectStringValue(value) };
        }

        return new object[] { GetObjectStringValue(value) };
    }

    private static void ReplaceInParts(
        List<object> parts, string marker, IReadOnlyList<object> replacements)
    {
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i] is not string text)
            {
                continue;
            }

            var index = text.IndexOf(marker, StringComparison.Ordinal);

            if (index < 0)
            {
                continue;
            }

            var replaced = new List<object>();
            var position = 0;

            while (index >= 0)
            {
                if (index > position)
                {
                    replaced.Add(text.Substring(position, index - position));
                }

                for (var r = 0; r < replacements.Count; r++)
                {
                    replaced.Add(replacements[r]);
                }

                position = index + marker.Length;
                index = text.IndexOf(marker, position, StringComparison.Ordinal);
            }

            if (position < text.Length)
            {
                replaced.Add(text.Substring(position));
            }

            parts.RemoveAt(i);
            parts.InsertRange(i, replaced);

            i += replaced.Count - 1;
        }
    }

    /// <summary>
    /// The text a substituted value becomes. A type never reaches here.
    /// </summary>
    /// <remarks>
    /// It used to: a type was turned into its short name at this point, which is when the tree is
    /// being built and before any output mode exists. There is no answer to give then - the same
    /// tree is meant to be writable as <c>Result</c> or as <c>global::Sample.Models.Result</c> - so
    /// the caller keeps the type and this only ever sees things that really are values.
    /// </remarks>
    private string GetObjectStringValue(object value)
    {
        if (value is string stringValue)
        {
            return LiteralFormatter.QuoteString(stringValue);
        }

        return LiteralFormatter.Format(value);
    }

    public SwitchBlockDefinition Switch(object switchValue)
    {
        var switchStatement = new SwitchBlockDefinition(CodeOutputComponent.Get(switchValue));

        StatementList.Add(switchStatement);

        return switchStatement;
    }

    public virtual void NewLine()
    {
        AddCode("");
    }

    public TryCatchBlock Try()
    {
        return Add(new TryCatchBlock());
    }

    public void Throw(Type type, params object[] parameters)
    {
        Add(new PostfixOutputComponent(";\n", new ThrowNewExceptionStatement(TypeDefinition.Get(type), parameters)));
    }

    public void Throw(ITypeDefinition exceptionType, params object[] parameters)
    {
        Add(new PostfixOutputComponent(";\n", new ThrowNewExceptionStatement(exceptionType, parameters)));
    }

    public void Return(object? returnValue = null)
    {
        if (returnValue == null)
        {
            AddIndentedStatement("return");
        }
        else
        {
            AddIndentedStatement(
                new AppendStatement("return ", CodeOutputComponent.Get(returnValue)));
        }
    }

    public void Break()
    {
        AddIndentedStatement("break");
    }

    /// <summary>
    /// <c>continue;</c> - the other half of <see cref="Break"/>, which had no equivalent, so the
    /// only way to skip an iteration was to write the statement out as text.
    /// </summary>
    public void Continue()
    {
        AddIndentedStatement("continue");
    }

    public WhileDefinition While(object testStatement)
    {
        return Add(new WhileDefinition(testStatement));
    }

    public ForEachDefinition ForEach(string variable, IOutputComponent enumerableComponent)
    {
        return Add(new ForEachDefinition(variable, enumerableComponent));
    }

    /// <summary>
    /// A counting loop, <c>for(var i = 0; i &lt; limit; i++)</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="ForDefinition"/> existed but wrote nothing and nothing returned one, so a
    /// <c>for</c> loop had to be hand-written through <see cref="AddCode(string, object[])"/>.
    /// </remarks>
    public ForDefinition For(string variable, object startValue, object exclusiveLimit)
    {
        return Add(new ForDefinition(variable, startValue, exclusiveLimit));
    }

    /// <summary>
    /// A loop with all three clauses given directly. Any of them may be null.
    /// </summary>
    public ForDefinition For(
        IOutputComponent? initializer, IOutputComponent? condition, IOutputComponent? increment)
    {
        return Add(new ForDefinition(initializer, condition, increment));
    }

    public IfElseLogicBlockDefinition If(string ifStatement)
    {
        return Add(new IfElseLogicBlockDefinition(new CodeOutputComponent(ifStatement) { Indented = false }));
    }

    public IfElseLogicBlockDefinition If(IOutputComponent outputComponent)
    {
        return Add(new IfElseLogicBlockDefinition(outputComponent));
    }

    public ToClass Assign(string value)
    {
        return new ToClass(c => StatementList.Add(c), new CodeOutputComponent(value) { Indented = false });
    }
        
    public ToClass Assign(IOutputComponent value)
    {
        return new ToClass(c => StatementList.Add(c), value);
    }

    public class ToClass
    {
        private readonly IOutputComponent _valueComponent;
        private readonly Action<IOutputComponent> _addStatement;

        public ToClass(Action<IOutputComponent> addStatement, IOutputComponent valueComponent)
        {
            _addStatement = addStatement;
            _valueComponent = valueComponent;
        }

        public void To(IOutputComponent outputComponent)
        {

            _addStatement(new AssignmentStatement(_valueComponent, outputComponent));
        }

        public void To(string destination)
        {
            To(new CodeOutputComponent(destination) { Indented = false });
        }

        public InstanceDefinition ToVar(string name)
        {
            var newLocalVariableDefinition = new InstanceDefinition(name){ Indented = false };

            var assignmentStatement = 
                new AssignmentStatement(_valueComponent, newLocalVariableDefinition) { Indented = false };

            _addStatement(new VarStatement(assignmentStatement));

            return newLocalVariableDefinition;
        }

        public InstanceDefinition ToLocal(ITypeDefinition typeDefinition, string name)
        {
            var newLocalVariableDefinition = new InstanceDefinition(name) { Indented = false };

            var assignmentStatement =
                new AssignmentStatement(_valueComponent, newLocalVariableDefinition) { Indented = false };

            _addStatement(new DeclarationStatement(typeDefinition, assignmentStatement));

            return newLocalVariableDefinition;
        }
    }

    protected void WriteBlock(IOutputContext context)
    {
        context.OpenScope();

        foreach (var outputComponent in StatementList)
        {
            outputComponent.WriteOutput(context);
        }

        context.CloseScope();
    }
}