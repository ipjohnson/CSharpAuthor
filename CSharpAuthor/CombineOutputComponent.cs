using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class CombineOutputComponent : BaseOutputComponent
    {
        private readonly IList<IOutputComponent> _components;
        public CombineOutputComponent(params object[] components)
        {
            _components = new List<IOutputComponent>();

            foreach (var component in components)
            {
                _components.Add(CodeOutputComponent.Get(component));
            }
        }

        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            foreach (var component in _components)
            {
                component.WriteOutput(outputContext);
            }
        }
    }
}
