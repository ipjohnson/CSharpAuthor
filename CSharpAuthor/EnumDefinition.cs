using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class EnumDefinition : BaseOutputComponent
    {
        private readonly List<EnumValueDefinition> _enumValueDefinitions = new ();
        private readonly string _enumName;

        public EnumDefinition(string enumName)
        {
            _enumName = enumName;
        }

        public ITypeDefinition? BaseType { get; set; }

        public EnumDefinition AddFlags()
        {
            AddAttribute(TypeDefinition.Get(typeof(FlagsAttribute)));

            return this;
        }

        public EnumValueDefinition AddValue(string enumValueName)
        {
            var enumValueDefinition = new EnumValueDefinition(enumValueName);

            _enumValueDefinitions.Add(enumValueDefinition);

            return enumValueDefinition;
        }

        public EnumValueDefinition AddValue(string enumValueName, object value)
        {
            var enumValueDefinition = AddValue(enumValueName);

            enumValueDefinition.Value = value;

            return enumValueDefinition;
        }

        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            WriteEnumSignature(outputContext);

            outputContext.OpenScope();

            foreach (var enumValueDefinition in _enumValueDefinitions)
            {
                enumValueDefinition.WriteOutput(outputContext);
            }

            outputContext.CloseScope();
        }

        private void WriteEnumSignature(IOutputContext outputContext)
        {
            var modifier =  GetAccessModifier("public");

            outputContext.WriteIndent();
            outputContext.Write($"{modifier} enum {_enumName}");

            if (BaseType != null)
            {
                outputContext.Write(" : ");
                outputContext.Write(BaseType);
            }

            outputContext.WriteLine();
        }
    }
}
