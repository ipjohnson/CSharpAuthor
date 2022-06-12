using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class AssignmentStatement : BaseOutputComponent
    {
        private IOutputComponent _valueComponent;
        private IOutputComponent _destinationComponent;

        public AssignmentStatement(IOutputComponent valueComponent, IOutputComponent destinationComponent)
        {
            _valueComponent = valueComponent;
            _destinationComponent = destinationComponent;
        }

        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            outputContext.WriteIndent();
            _destinationComponent.WriteOutput(outputContext);
            outputContext.Write(" = ");
            _valueComponent.WriteOutput(outputContext);
            outputContext.WriteLine(";");
        }
    }
}
