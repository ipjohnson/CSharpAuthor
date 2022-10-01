using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class PrefixOutputComponent : BaseOutputComponent
    {
        private readonly string _prefix;
        private readonly IOutputComponent _awaitableOutputComponent;

        public PrefixOutputComponent(string prefix, IOutputComponent awaitableOutputComponent)
        {
            _prefix = prefix;
            _awaitableOutputComponent = awaitableOutputComponent;
        }

        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            outputContext.Write(_prefix);
            _awaitableOutputComponent.WriteOutput(outputContext);
        }
    }
}
