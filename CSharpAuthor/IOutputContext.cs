using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpAuthor
{
    public interface IOutputContext
    {
        string IndentString { get; }

        void IncrementIndent();

        void DecrementIndent();

        void Write(string text);

        void WriteLine(string text);

        void WriteSpace() => Write(" ");

        void WriteIndentedLine(string text);

        string Output();

        void OpenScope();

        void CloseScope();
    }
}
