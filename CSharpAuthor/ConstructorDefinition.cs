namespace CSharpAuthor
{
    public class ConstructorDefinition : MethodDefinition
    {
        public string BaseStatement { get; set; }

        public ConstructorDefinition(string name) : base(name)
        {
        }

        protected override void WriteReturnType(IOutputContext outputContext)
        {
            // constructors don't have return Types
        }

        protected override void WriteAccessModifier(IOutputContext outputContext)
        {
            outputContext.WriteIndent();
            outputContext.Write(GetAccessModifier(KeyWords.Public));
            outputContext.WriteSpace();
        }

        protected override void WriteEndOfMethodSignature(IOutputContext outputContext)
        {
            base.WriteEndOfMethodSignature(outputContext);

            if (!string.IsNullOrEmpty(BaseStatement))
            {
                outputContext.WriteIndent();
                outputContext.Write(outputContext.SingleIndent);
                outputContext.Write(" : ");
                outputContext.Write(BaseStatement);
                outputContext.WriteLine();
            }
        }
    }
}
