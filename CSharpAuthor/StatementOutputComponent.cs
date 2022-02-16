namespace CSharpAuthor
{
    public class StatementOutputComponent : BaseOutputComponent
    {
        private string statement;

        public StatementOutputComponent(string statement)
        {
            this.statement = statement;
        }
        
        public override void WriteOutput(IOutputContext outputContext)
        {
            outputContext.WriteIndentedLine(statement);
        }
    }
}
