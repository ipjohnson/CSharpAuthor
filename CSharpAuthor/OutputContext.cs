﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpAuthor;

public class OutputContextOptions
{
    public char IndentChar { get; set; } = ' ';

    public int IndentCharCount { get; set; } = 4;

    public string NewLine { get; set; } = "\n";
}

public class OutputContext : IOutputContext
{
    private readonly HashSet<string> _namespaces = new ();
    private readonly StringBuilder _output;
    private int _indentIndex;
    private readonly OutputContextOptions _options;

    public OutputContext(OutputContextOptions? options = null)
    {
        this._options = options ?? new OutputContextOptions();
        
        _output = new StringBuilder();
        IndentString = "";
    }

    public string SingleIndent => new (_options.IndentChar, _options.IndentCharCount);

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
        AddImportNamespace(typeDefinition);

        typeDefinition?.WriteShortName(_output);
    }

    public void WriteLine()
    {
        _output.Append(_options.NewLine);
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
            _output.Insert(0, _options.NewLine);

            foreach (string ns in namespaceList)
            {
                _output.Insert(0, $"using {ns};" + _options.NewLine);
            }
        }
    }

    public void WriteIndentedLine(string text)
    {
        _output.Append(IndentString);
        _output.AppendLine(text);
    }

    private void SetIndentString()
    {
        IndentString = new string(_options.IndentChar, _options.IndentCharCount * _indentIndex);
    }
}