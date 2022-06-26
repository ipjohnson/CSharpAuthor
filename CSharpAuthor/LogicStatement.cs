﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpAuthor
{
    public class LogicStatement : BaseOutputComponent
    {
        private readonly string _logicStatement;
        private readonly IReadOnlyList<IOutputComponent> _outputComponents;

        public LogicStatement(string logicStatement, params object[] outputComponents)
        {
            _logicStatement = logicStatement;
            _outputComponents = CodeOutputComponent.GetAll(outputComponents).ToList();
        }

        public bool PrintParentheses { get; set; } = true;

        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            if (PrintParentheses)
            {
                outputContext.Write("(");
            }

            _outputComponents.OutputSeparatedList(outputContext, (context, component) => component.WriteOutput(context), _logicStatement);
            
            if (PrintParentheses)
            {
                outputContext.Write(")");
            }
        }
    }
}
