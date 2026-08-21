using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpAuthor;

public enum TypeOutputMode
{
    Global,
    FullName,
    ShortName,
}

public class OutputContextOptions
{
    public char IndentChar { get; set; } = ' ';

    public int IndentCharCount { get; set; } = 4;

    public string NewLine { get; set; } = "\n";
    
    public bool BreakInvokeLines { get; set; } = true;
    
    public bool GenerateDocumentation { get; set; } = true;
    
    public TypeOutputMode TypeOutputMode { get; set; } = TypeOutputMode.ShortName;
}

public class OutputContext : IOutputContext
{
    private readonly HashSet<string> _namespaces = new ();
    private readonly StringBuilder _output;
    private int _indentIndex;
    private bool _usingStatementsGenerated;
    public OutputContextOptions Options { get; }

    public OutputContext(OutputContextOptions? options = null)
    {
        Options = options ?? new OutputContextOptions();
        
        _output = new StringBuilder();
        IndentString = "";
    }

    public string SingleIndent => new (Options.IndentChar, Options.IndentCharCount);

    public string IndentString { get; private set; }

    public void IncrementIndent()
    {
        _indentIndex++;
        SetIndentString();
    }

    public void DecrementIndent()
    {
        _indentIndex--;
        SetIndentString();
    }

    public void Write(string text)
    {
        _output.Append(text);
    }

    public void Write(ITypeDefinition typeDefinition)
    {
        if (Options.TypeOutputMode == TypeOutputMode.ShortName)
        {
            AddImportNamespace(typeDefinition);
        }
        
        typeDefinition?.WriteTypeName(_output, Options.TypeOutputMode);
    }

    public void WriteLine()
    {
        _output.Append(Options.NewLine);
    }

    /// <remarks>
    /// <see cref="StringBuilder.AppendLine(string)"/> appends <see cref="Environment.NewLine"/>,
    /// which is the host's line ending rather than the configured one. That made generated output
    /// differ by the operating system it was generated on, and left
    /// <see cref="OutputContextOptions.NewLine"/> reaching almost nothing it claimed to control.
    /// </remarks>
    public void WriteLine(string text)
    {
        _output.Append(text);
        _output.Append(Options.NewLine);
    }

    public void WriteSpace()
    {
        _output.Append(" ");
    }

    public void WriteIndent(string text = "")
    {
        _output.Append(IndentString);
        _output.Append(text);
    }

    public string Output()
    {
        return _output.ToString();
    }

    public void OpenScope()
    {
        WriteIndentedLine("{");
        IncrementIndent();
    }

    public void CloseScope()
    {
        DecrementIndent();
        WriteIndentedLine("}");
    }

    public void AddImportNamespace(string ns)
    {
        if (string.IsNullOrEmpty(ns) || _namespaces.Contains(ns))
        {
            return;
        }

        _namespaces.Add(ns);
    }

    public void AddImportNamespace(ITypeDefinition typeDefinition)
    {
        foreach (var knownNamespace in typeDefinition.KnownNamespaces)
        {
            AddImportNamespace(knownNamespace);
        }
    }

    public void AddImportNamespaces(IEnumerable<string> namespaces)
    {
        foreach (var ns in namespaces)
        {
            AddImportNamespace(ns);
        }
    }

    public void AddImportNamespaces(IEnumerable<ITypeDefinition> typeDefinitions)
    {
        foreach (var typeDefinition in typeDefinitions)
        {
            AddImportNamespace(typeDefinition);
        }
    }

    /// <remarks>
    /// Idempotent, because <see cref="CSharpFileDefinition"/> already calls this and nothing says
    /// so at the call site. Calling it again used to insert the whole block a second time, for a
    /// file that still compiled and warned CS0105 on every duplicated directive.
    /// </remarks>
    public void GenerateUsingStatements()
    {
        if (_usingStatementsGenerated)
        {
            return;
        }

        _usingStatementsGenerated = true;

        var namespaceList = _namespaces.ToList();

        namespaceList.Sort();

        namespaceList.Reverse();

        if (namespaceList.Count > 0)
        {
            _output.Insert(0, Options.NewLine);

            foreach (string ns in namespaceList)
            {
                _output.Insert(0, $"using {ns};" + Options.NewLine);
            }
        }
    }

    public char? LastCharacter
    {
        get
        {
            if (_output.Length == 0)
            {
                return null;
            }
            
            return _output[_output.Length - 1];
        }
    }

    /// <inheritdoc cref="WriteLine(string)"/>
    public void WriteIndentedLine(string text)
    {
        _output.Append(IndentString);
        _output.Append(text);
        _output.Append(Options.NewLine);
    }

    private void SetIndentString()
    {
        IndentString = new string(Options.IndentChar, Options.IndentCharCount * _indentIndex);
    }
}