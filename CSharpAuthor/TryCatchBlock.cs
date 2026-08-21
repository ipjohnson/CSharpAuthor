using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// A <c>try</c> block with its <c>catch</c> clauses and <c>finally</c>.
/// </summary>
/// <remarks>
/// Statements added to this object are the <c>try</c> body; <see cref="Catch(ITypeDefinition, string, IOutputComponent)"/>
/// and <see cref="Finally"/> each return a block of their own. Clauses are written in the order
/// they were added, which matters - C# requires the more derived exception type first, and nothing
/// here reorders them.
/// </remarks>
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

    /// <summary>
    /// A <c>catch</c> clause, optionally naming the exception and filtering it with <c>when</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An empty <paramref name="name"/> gives <c>catch (Exception)</c> - a clause that catches
    /// without binding, which is what a rethrow wants. <paramref name="when"/> is written inside
    /// its own parentheses, so pass the condition without them.
    /// </para>
    /// <example>
    /// <code>
    /// var attempt = method.Try();
    /// attempt.AddIndentedStatement("Work()");
    /// attempt.Catch(typeof(Exception), "e").AddIndentedStatement("Log(e)");
    /// attempt.Finally().AddIndentedStatement("Cleanup()");
    /// </code>
    /// which is
    /// <code>
    /// try
    /// {
    ///     Work();
    /// }
    /// catch (Exception e)
    /// {
    ///     Log(e);
    /// }
    /// finally
    /// {
    ///     Cleanup();
    /// }
    /// </code>
    /// </example>
    /// <para>
    /// A bare rethrow is <c>AddIndentedStatement("throw")</c> on the returned block;
    /// <see cref="BaseBlockDefinition.Throw(ITypeDefinition, object[])"/> always constructs a new
    /// exception.
    /// </para>
    /// </remarks>
    public BaseBlockDefinition Catch(ITypeDefinition exceptionType, string name = "", IOutputComponent? when = null)
    {
        var catchBlock = new CatchBlock(exceptionType, name, when);
            
        _catchBlocks.Add(catchBlock);
            
        return catchBlock;
    }

    /// <summary>
    /// The <c>finally</c> clause. Statements go on the block this returns.
    /// </summary>
    /// <remarks>
    /// A second call returns a second block and the first is discarded, so ask once and keep what
    /// comes back.
    /// </remarks>
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