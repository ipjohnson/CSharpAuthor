using static CSharpAuthor.SyntaxHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CSharpAuthor.Tests.Switch;

public class SwitchBlockDefinitionTests
{
    [Fact]
    public void SimpleSwitchStatement()
    {
        var switchStatement = new SwitchBlockDefinition(CodeOutputComponent.Get("switchValue"));

        switchStatement.AddCase(QuoteString("a"));
        switchStatement.AddCase(QuoteString("b"));
        var cBlock = switchStatement.AddCase(QuoteString("c"));

        cBlock.Return(QuoteString("abc"));
            
        var dBlock = switchStatement.AddCase(QuoteString("d"));
        dBlock.Break();

        switchStatement.AddDefault().Return(QuoteString("Other"));

        var outputContext = new OutputContext();

        switchStatement.WriteOutput(outputContext);

        AssertEqual.WithoutNewLine(SimpleSwitchStatementExpected, outputContext.Output());
    }

    private const string SimpleSwitchStatementExpected = 
        @"switch (switchValue)
{
    case ""a"":
    case ""b"":
    case ""c"":
        return ""abc"";
    case ""d"":
        break;
    default:
        return ""Other"";
}
";
}