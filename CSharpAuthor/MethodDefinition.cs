using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CSharpAuthor;

public class MethodDefinition : BaseBlockDefinition, INamedComponent
{
    protected readonly List<ParameterDefinition> ParameterList = new ();
    private readonly List<ITypeDefinition> _genericParameters = new();
    private readonly List<ConstraintDefinition> _constraints = new();
    protected int VariableCount = 1;
        
    private ITypeDefinition? _returnType;
        
    public MethodDefinition(string name)
    {
        Name = name;
    }

    public string Name { get; }
    
    public string? ReturnComment { get; set; }

    public List<ITypeDefinition> GenericParameters => _genericParameters;

    /// <remarks>
    /// A clause the caller has already rendered. <see cref="AddConstraint"/> builds one part by part
    /// instead, and the two can be used together — this is written first.
    /// </remarks>
    public IOutputComponent? WhereStatement { get; set; }

    /// <summary>
    /// The constraints declared through <see cref="AddConstraint"/>.
    /// </summary>
    public IReadOnlyList<ConstraintDefinition> Constraints => _constraints;

    /// <summary>
    /// Constrains one of this method's type parameters, written after the parameter list.
    /// </summary>
    /// <remarks>
    /// Returns the constraint so its parts can be added in any order; they are written in the order
    /// C# requires. Calling this twice for one parameter returns the same constraint rather than
    /// declaring a second <c>where</c> for it, which would not compile.
    /// </remarks>
    public ConstraintDefinition AddConstraint(string typeParameter)
    {
        foreach (var existing in _constraints)
        {
            if (existing.TypeParameter == typeParameter)
            {
                return existing;
            }
        }

        var constraint = new ConstraintDefinition(typeParameter);

        _constraints.Add(constraint);

        return constraint;
    }
    
    public ITypeDefinition? InterfaceImplementation { get; set; }

    public ITypeDefinition? ReturnType => _returnType;

    public IReadOnlyList<ParameterDefinition> Parameters => ParameterList;

    public string GetUniqueVariable(string prefix)
    {
        return prefix + (VariableCount++).ToString(CultureInfo.InvariantCulture);
    }

    public void AddGenericParameter(ITypeDefinition typeDefinition)
    {
        _genericParameters.Add(typeDefinition);
    }

    public MethodDefinition SetReturnType(Type type)
    {
        return SetReturnType(TypeDefinition.Get(type));
    }

    public MethodDefinition SetReturnType(ITypeDefinition type)
    {
        _returnType = type;

        return this;
    }

    public ParameterDefinition AddParameter(Type type, string name)
    {
        return AddParameter(TypeDefinition.Get(type), name);
    }

    public ParameterDefinition AddParameter(ITypeDefinition typeDefinition, string name)
    {
        var parameter = new ParameterDefinition(typeDefinition, name);

        ParameterList.Add(parameter);

        return parameter;
    }

    public MethodDefinition AddParameter(ParameterDefinition parameterDefinition)
    {
        ParameterList.Add(parameterDefinition);
        
        return this;
    }
    
    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        ProcessNamespaces(outputContext);

        WriteMethodSignature(outputContext);

        WriteMethodBody(outputContext);
    }
    
    protected override void WriteComment(IOutputContext outputContext)
    {
        if (string.IsNullOrEmpty(Comment))
        {
            return;
        }
        
        DocumentationComment.WriteSummary(outputContext.WriteIndentedLine, Comment);

        foreach (ParameterDefinition parameterDefinition in ParameterList)
        {
            if (parameterDefinition.Comment != null)
            {
                DocumentationComment.WriteElement(
                    outputContext.WriteIndentedLine,
                    "<param name=\"" + parameterDefinition.Name + "\">",
                    "</param>",
                    parameterDefinition.Comment);
            }
        }

        if (ReturnComment != null)
        {
            DocumentationComment.WriteElement(
                outputContext.WriteIndentedLine, "<returns>", "</returns>", ReturnComment);
        }
    }

    private void ProcessNamespaces(IOutputContext outputContext)
    {
        if (_returnType != null)
        {
            outputContext.AddImportNamespace(_returnType);
        }

        if (InterfaceImplementation != null)
        {
            outputContext.AddImportNamespace(InterfaceImplementation);
        }
    }

    /// <summary>
    /// Declares the method without a body, terminating it with <c>;</c>.
    /// </summary>
    /// <remarks>
    /// The defining half of a <c>partial</c> method, or an <c>extern</c> one. <c>abstract</c>
    /// implies it, so that does not need setting as well.
    /// </remarks>
    public bool OmitBody { get; set; }

    /// <summary>
    /// Whether this declaration ends at the signature.
    /// </summary>
    /// <remarks>
    /// An abstract method used to be written with <c>{ }</c> after it, which is CS0500 - "cannot
    /// declare a body because it is marked abstract". The modifier was being dropped at the same
    /// time, so the result compiled as an ordinary empty method and the abstraction quietly
    /// disappeared instead.
    /// </remarks>
    private bool IsBodyless =>
        OmitBody || (Modifiers & ComponentModifier.Abstract) == ComponentModifier.Abstract;

    protected virtual void WriteMethodBody(IOutputContext outputContext)
    {
        if (IsBodyless)
        {
            return;
        }

        WriteBlock(outputContext);
    }

    protected virtual void WriteMethodSignature(IOutputContext outputContext)
    {
        WriteAccessModifier(outputContext);

        WriteReturnType(outputContext);

        outputContext.WriteSpace();

        if (InterfaceImplementation != null)
        {
            outputContext.Write(InterfaceImplementation);
            outputContext.Write(".");
        }

        outputContext.Write(CSharpIdentifier.Escape(Name));

        if (_genericParameters.Count > 0)
        {
            outputContext.Write("<");
            _genericParameters.OutputCommaSeparatedList(outputContext);
            outputContext.Write(">");
        }
            
        outputContext.Write("(");

        for (var i = 0; i < ParameterList.Count; i++)
        {
            if (i > 0)
            {
                outputContext.Write(", ");
            }

            ParameterList[i].WriteWithSignature(outputContext);
        }

        outputContext.Write(")");

        WriteEndOfMethodSignature(outputContext);
    }

    protected virtual void WriteEndOfMethodSignature(IOutputContext outputContext)
    {
        WhereStatement?.WriteOutput(outputContext);

        foreach (var constraint in _constraints)
        {
            if (constraint.IsEmpty)
            {
                continue;
            }

            outputContext.WriteSpace();

            constraint.WriteOutput(outputContext);
        }

        if (IsBodyless)
        {
            outputContext.Write(";");
        }

        outputContext.WriteLine();
    }

    protected virtual void WriteAccessModifier(IOutputContext outputContext)
    {
        outputContext.WriteIndent();

        // An explicit interface implementation takes no accessibility and none of the inheritance
        // modifiers - the interface decides all of that - but it can still be async.
        if (InterfaceImplementation != null)
        {
            outputContext.Write(
                Modifiers.GetModifierKeywords(ComponentModifier.Async));

            return;
        }

        outputContext.Write(GetAccessModifier(KeyWords.Public));
        outputContext.WriteSpace();

        outputContext.Write(
            Modifiers.GetModifierKeywords(ComponentModifierExtensions.MethodModifiers));
    }

    protected virtual void WriteReturnType(IOutputContext outputContext)
    {
        if (_returnType != null)
        {
            outputContext.Write(_returnType);
        }
        else
        {
            outputContext.Write("void");
        }
    }
}