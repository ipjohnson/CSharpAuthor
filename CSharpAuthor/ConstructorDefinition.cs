namespace CSharpAuthor
{
    public class ConstructorDefinition : MethodDefinition
    {

        public ConstructorDefinition(string name) : base(name)
        {
        }

        protected override void WriteReturnType(IOutputContext outputContext)
        {
            // constructors don't have return Types
        }

        protected override void WriteAccessModifier(IOutputContext outputContext)
        {
            outputContext.Write(GetAccessModifier(KeyWords.Public));
            outputContext.WriteSpace();
        }
    }
}
