namespace CSharpAuthor
{
    public abstract class BaseOutputComponent : IOutputComponent
    {
        public ComponentModifier Modifiers { get; set; } = ComponentModifier.None;

        public string? Comment { get; set; }

        public abstract void WriteOutput(IOutputContext outputContext);

        protected string GetAccessModifier(string defaultString)
        {
            if ((Modifiers & ComponentModifier.Public) == ComponentModifier.Public)
            {
                return KeyWords.Public;
            }

            if ((Modifiers & ComponentModifier.Protected) == ComponentModifier.Protected)
            {
                return KeyWords.Protected;
            }

            if ((Modifiers & ComponentModifier.Private) == ComponentModifier.Private)
            {
                return KeyWords.Private;
            }

            return defaultString;
        }
    }
}
