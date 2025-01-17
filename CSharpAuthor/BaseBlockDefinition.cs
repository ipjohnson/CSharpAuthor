﻿using System;
using System.Collections.Generic;
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

    public virtual CodeOutputComponent AddCode(string statement, params object[] types)
    {
        var typeDefinitions = new List<ITypeDefinition>();

        if (types is { Length: > 0 })
        {
            for (var index = 0; index < types.Length; index++)
            {
                var value = types[index];
                var typeSwapString = "{arg" + (index + 1) + "}";

                if (statement.IndexOf(typeSwapString, StringComparison.CurrentCulture) >= 0)
                {

                    if (value is Type typeValue)
                    {
                        value = TypeDefinition.Get(typeValue);
                    }

                    if (value is ITypeDefinition typeDefinition)
                    {
                        typeDefinitions.Add(typeDefinition);
                    }

                    statement = statement.Replace(typeSwapString, GetObjectStringValue(value));
                }
                else
                {
                    var rawSwapString = $"[arg{index + 1}]";

                    if (statement.IndexOf(rawSwapString, StringComparison.CurrentCulture) >= 0)
                    {
                        statement = statement.Replace(rawSwapString, value.ToString());
                    }
                }
            }
        }

        var statementOutput = new CodeOutputComponent(statement);

        statementOutput.AddTypes(typeDefinitions);

        return Add(statementOutput);
    }

    private string GetObjectStringValue(object value)
    {
        if (value is Type type)
        {
            value = TypeDefinition.Get(type);
        }

        if (value is ITypeDefinition typeDefinition)
        {
            return typeDefinition.GetShortName();
        }

        if (value is string stringValue)
        {
            return "\"" + stringValue + "\"";
        }

        return value.ToString();
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

    public WhileDefinition While(object testStatement)
    {
        return Add(new WhileDefinition(testStatement));
    }

    public ForEachDefinition ForEach(string variable, IOutputComponent enumerableComponent)
    {
        return Add(new ForEachDefinition(variable, enumerableComponent));
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