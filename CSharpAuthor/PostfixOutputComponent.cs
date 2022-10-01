using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class PostfixOutputComponent : BaseOutputComponent
    {
        private readonly string _postfix;
        private readonly IOutputComponent _outputComponent;

        public PostfixOutputComponent(string postfix, IOutputComponent outputComponent)
        {
            _postfix = postfix;
            _outputComponent = outputComponent;
        }

        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            _outputComponent.WriteOutput(outputContext);
            outputContext.Write(_postfix);
        }
    }
}
