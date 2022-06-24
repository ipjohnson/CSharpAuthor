using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class ListOutputComponent : BaseOutputComponent
    {
        private readonly IReadOnlyList<IOutputComponent> _components;

        public ListOutputComponent(IReadOnlyList<IOutputComponent> components)
        {
            _components = components;
        }

        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            _components.OutputCommaSeparatedList(outputContext);
        }
    }
}
