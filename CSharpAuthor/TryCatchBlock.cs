using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public class TryCatchBlock : BaseBlockDefinition
    {
        private readonly List<CatchBlock> _catchBlocks = new ();
        private FinallyBlock? _finallyBlock;
        
        public BaseBlockDefinition Catch(Type exceptionType, string name = "", IOutputComponent? when = null)
        {
            return Catch(TypeDefinition.Get(exceptionType), name);
        }

        public BaseBlockDefinition Catch(ITypeDefinition exceptionType, string name = "", IOutputComponent? when = null)
        {
            var catchBlock = new CatchBlock(exceptionType, name, when);
            
            _catchBlocks.Add(catchBlock);
            
            return catchBlock;
        }

        public BaseBlockDefinition Finally()
        {
            return _finallyBlock = new FinallyBlock();
        }

        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            outputContext.WriteIndentedLine("try");
            WriteBlock(outputContext);

            foreach (var catchBlock in _catchBlocks)
            {
                catchBlock.WriteOutput(outputContext);
            }

            _finallyBlock?.WriteOutput(outputContext);
        }

        private class CatchBlock : BaseBlockDefinition
        {
            private ITypeDefinition _exceptionType;
            private string _name;
            private IOutputComponent? _when;

            public CatchBlock(ITypeDefinition exceptionType, string name, IOutputComponent? when)
            {
                _exceptionType = exceptionType;
                _name = name;
                _when = when;
            }

            protected override void WriteComponentOutput(IOutputContext outputContext)
            {
                outputContext.WriteIndent("catch (");
                outputContext.Write(_exceptionType);

                if (!string.IsNullOrEmpty(_name))
                {
                    outputContext.WriteSpace();
                    outputContext.Write(_name);
                }
                outputContext.Write(")");

                if (_when != null)
                {
                    outputContext.Write(" when (");
                    _when.WriteOutput(outputContext);
                    outputContext.Write(")");
                }

                outputContext.WriteLine();
                WriteBlock(outputContext);
            }
        }
        
        private class FinallyBlock : BaseBlockDefinition
        {
            protected override void WriteComponentOutput(IOutputContext outputContext)
            {
                outputContext.WriteIndentedLine("finally");
                WriteBlock(outputContext);
            }
        }
    }
}
