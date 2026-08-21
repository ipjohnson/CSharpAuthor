using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class TryCatchBlock : BaseBlockDefinition
{
    private readonly List<CatchBlock> _catchBlocks = new ();
    private FinallyBlock? _finallyBlock;
        
    /// <summary>
    /// A <c>catch</c> clause, optionally naming the exception and filtering it with <c>when</c>.
    /// </summary>
    /// <remarks>
    /// This overload accepted a filter and then did not pass it on, so a caller who wrote one got a
    /// clause that caught everything of that type. A filter is the difference between handling an
    /// exception and swallowing it, and dropping it changes which handler runs.
    /// </remarks>
    public BaseBlockDefinition Catch(Type exceptionType, string name = "", IOutputComponent? when = null)
    {
        return Catch(TypeDefinition.Get(exceptionType), name, when);
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
        private readonly ITypeDefinition _exceptionType;
        private readonly string _name;
        private readonly IOutputComponent? _when;

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