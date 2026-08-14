using System;
using System.Collections.Generic;
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
        return prefix + VariableCount++;
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

    protected virtual void WriteMethodBody(IOutputContext outputContext)
    {
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

        outputContext.Write(Name);

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
        
        outputContext.WriteLine();
    }

    protected virtual void WriteAccessModifier(IOutputContext outputContext)
    {
        outputContext.WriteIndent();

        if (InterfaceImplementation == null)
        {
            outputContext.Write(GetAccessModifier(KeyWords.Public));
            outputContext.WriteSpace();

            if ((Modifiers & ComponentModifier.Static) == ComponentModifier.Static)
            {
                outputContext.Write(KeyWords.Static);
                outputContext.WriteSpace();
            }
            else if ((Modifiers & ComponentModifier.Abstract) == ComponentModifier.Abstract)
            {
                outputContext.Write(KeyWords.Abstract);
                outputContext.WriteSpace();
            }
            else if ((Modifiers & ComponentModifier.Virtual) == ComponentModifier.Virtual)
            {
                outputContext.Write(KeyWords.Virtual);
                outputContext.WriteSpace();
            }
            else if ((Modifiers & ComponentModifier.Override) == ComponentModifier.Override)
            {
                outputContext.Write(KeyWords.Override);
                outputContext.WriteSpace();
            }
        }
            
        if ((Modifiers & ComponentModifier.Async) == ComponentModifier.Async)
        {
            outputContext.Write(KeyWords.Async);
            outputContext.WriteSpace();
        }
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