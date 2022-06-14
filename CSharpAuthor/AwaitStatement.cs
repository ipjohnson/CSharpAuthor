using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class AwaitStatement : BaseOutputComponent
    {
        private readonly IOutputComponent _awaitableOutputComponent;

        public AwaitStatement(IOutputComponent awaitableOutputComponent)
        {
            _awaitableOutputComponent = awaitableOutputComponent;
        }

        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            outputContext.Write("await ");
            _awaitableOutputComponent.WriteOutput(outputContext);
        }
    }
}
