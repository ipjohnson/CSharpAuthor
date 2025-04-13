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
    
    public TypeOutputMode TypeOutputMode { get; set; } = TypeOutputMode.ShortName;
}

public class OutputContext : IOutputContext
{
    private readonly HashSet<string> _namespaces = new ();
    private readonly StringBuilder _output;
    private int _indentIndex;
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

    public void WriteLine(string text)
    {
        _output.AppendLine(text);
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

    public void GenerateUsingStatements()
    {
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

    public void WriteIndentedLine(string text)
    {
        _output.Append(IndentString);
        _output.AppendLine(text);
    }

    private void SetIndentString()
    {
        IndentString = new string(Options.IndentChar, Options.IndentCharCount * _indentIndex);
    }
}