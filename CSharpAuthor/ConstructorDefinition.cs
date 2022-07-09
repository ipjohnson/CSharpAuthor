namespace CSharpAuthor
{
    public class ConstructorDefinition : MethodDefinition
    {
        public IOutputComponent? Base { get; }

        public ConstructorDefinition(string name, IOutputComponent? @base = null) : base(name)
        {
            Base = @base;
        }

        protected override void WriteReturnType(IOutputContext outputContext)
        {
            // constructors don't have return Types
        }

        protected override void WriteAccessModifier(IOutputContext outputContext)
        {
            outputContext.WriteIndent();

            if ((Modifiers & ComponentModifier.Static) == ComponentModifier.Static)
            {
                outputContext.Write(KeyWords.Static);
            }
            else
            {
                outputContext.Write(GetAccessModifier(KeyWords.Public));
            }
        }

        protected override void WriteEndOfMethodSignature(IOutputContext outputContext)
        {
            base.WriteEndOfMethodSignature(outputContext);

            if (Base != null)
            {
                outputContext.WriteIndent();
                outputContext.Write(outputContext.SingleIndent);
                outputContext.Write(" : ");
                Base.WriteOutput(outputContext);
                outputContext.WriteLine();
            }
        }
    }
}
