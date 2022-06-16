using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class SwitchBlockDefinition : BaseOutputComponent
    {
        private readonly IOutputComponent _switchValue;
        private readonly List<CaseBlockDefinition> _cases = new ();
        private CaseBlockDefinition? _default;

        public SwitchBlockDefinition(IOutputComponent switchValue)
        {
            _switchValue = switchValue;
        }

        public CaseBlockDefinition AddDefault()
        {
            return _default ??= new CaseBlockDefinition (CodeOutputComponent.Get("default:"));
        }

        public CaseBlockDefinition AddCase(object value)
        {
            var caseBlock = new CaseBlockDefinition(WrapCaseStatement(value));

            _cases.Add(caseBlock);

            return caseBlock;
        }

        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            outputContext.WriteIndent("switch (");
            _switchValue.WriteOutput(outputContext);
            outputContext.WriteLine(")");
            outputContext.OpenScope();
            foreach (var caseBlockDefinition in _cases)
            {
                caseBlockDefinition.WriteOutput(outputContext);
            }
            _default?.WriteOutput(outputContext);
            outputContext.CloseScope();
        }

        private WrapStatement WrapCaseStatement(object value)
        {
            var outputComponent = CodeOutputComponent.Get(value);

            return new WrapStatement(outputComponent, "case ", ":");
        }
    }
}
