using CSharpAuthor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpAuthor
{
    public class ClassDefinition : BaseOutputComponent
    {
        private readonly List<IOutputComponent> fields = new ();
        private readonly List<IOutputComponent> constructors = new ();
        private readonly List<IOutputComponent> methods = new ();
        private readonly List<IOutputComponent> properties = new ();

        public FieldDefinition AddField(TypeDefinition typeDefinition, string fieldName)
        {
            var definition = new FieldDefinition(typeDefinition, fieldName);

            fields.Add(definition);

            return definition;
        }

        public ConstructorDefinition AddConstructor()
        {
            var definition = new ConstructorDefinition();

            constructors.Add(definition);

            return definition;
        }

        public override void GetKnownTypes(List<TypeDefinition> types)
        {
            ApplyAllComponents(component => component.GetKnownTypes(types));
        }

        public override void WriteOutput(IOutputContext outputContext)
        {
            WriteUsingStatements(outputContext);

            WriteClassOpening(outputContext);

            ApplyAllComponents(component => component.WriteOutput(outputContext));

            WriteClassClosing(outputContext);
        }

        private void ApplyAllComponents(Action<IOutputComponent> componentAction)
        {

            foreach (var field in fields)
            {
                componentAction(field);
            }

            foreach (var constructor in constructors)
            {
                componentAction(constructor);
            }

            foreach (var method in methods)
            {                
                componentAction(method);
            }

            foreach (var property in properties)
            {
                componentAction(property);
            }
        }

        private void WriteClassClosing(IOutputContext outputContext)
        {

        }

        private void WriteClassOpening(IOutputContext outputContext)
        {

        }

        private void WriteUsingStatements(IOutputContext outputContext)
        {

        }
    }
}
