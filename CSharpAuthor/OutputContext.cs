using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpAuthor
{
    public class OutputContext : IOutputContext
    {
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

        private void SetIndentString()
        {
            IndentString = new string(indentChar, indentCharCount * indentIndex);
        }

        public void WriteIndentedLine(string text)
        {
            output.Append(IndentString);
            output.AppendLine(text);
        }
    }
}
