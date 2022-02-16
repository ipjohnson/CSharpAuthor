using System.Text;

namespace CSharpAuthor
{
    public class OutputContext : IOutputContext
    {
        private readonly HashSet<string> namespaces = new ();
        private readonly char indentChar;
        private readonly int indentCharCount;
        private readonly StringBuilder output;
        private int indentIndex;

        public OutputContext(char indentChar = ' ', int indentCharCount = 4)
        {
            this.indentChar = indentChar;
            this.indentCharCount = indentCharCount;

            output = new StringBuilder();
            IndentString = "";
        }

        public string IndentString { get; private set; }

        public void IncrementIndent()
        {
            indentIndex++;
            SetIndentString();
        }

        public void DecrementIndent()
        {
            indentIndex--;
            SetIndentString();
        }

        public void Write(string text)
        {
            output.Append(text);
        }

        public void WriteLine(string text)
        {
            output.AppendLine(text);
        }

        public string Output()
        {
            return output.ToString();
        }

        public void OpenScope()
        {
            WriteIndentedLine("{");
            IncrementIndent();
        }

        public void CloseScope()
        {
            DecrementIndent();
            WriteIndentedLine("}");
        }

        public void AddImportNamespace(string ns)
        {
            if (string.IsNullOrEmpty(ns) || namespaces.Contains(ns))
            {
                return;
            }

            namespaces.Add(ns);
        }

        public void GenerateUsingStatements()
        {
            var namespaceList = namespaces.ToList();

            namespaceList.Sort();

            namespaceList.Reverse();

            foreach (string ns in namespaceList)
            {
                output.Insert(0, $"using {ns};" + Environment.NewLine);
            }
        }

        public void WriteIndentedLine(string text)
        {
            output.Append(IndentString);
            output.AppendLine(text);
        }

        private void SetIndentString()
        {
            IndentString = new string(indentChar, indentCharCount * indentIndex);
        }
    }
}
