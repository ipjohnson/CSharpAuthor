﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpAuthor;

public class PropertyMethodDefinition : MethodDefinition
{
    public PropertyMethodDefinition() : base("")
    {

    }

    public bool LambdaSyntax { get; set; }

    protected override void WriteMethodSignature(IOutputContext outputContext)
    {
        // don't write anything as it will be covered 
    }

    protected override void WriteMethodBody(IOutputContext outputContext)
    {
        if (LambdaSyntax)
        {
            outputContext.Write(" => ");
            var statement = StatementList.First();

            if (statement is CodeOutputComponent statementOutput)
            {
                statementOutput.Indented = false;
            }

            statement.WriteOutput(outputContext);

            if (outputContext.LastCharacter != ';')
            {
                outputContext.Write(";");
            }
            
            outputContext.WriteLine();
        }
        else
        {
            outputContext.WriteLine();
            base.WriteMethodBody(outputContext);
        }
    }
}