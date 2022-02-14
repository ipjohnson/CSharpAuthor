using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpAuthor
{
    public interface IOutputComponent
    {
        void WriteOutput(IOutputContext outputContext);

        void GetKnownTypes(List<TypeDefinition> types);
    }
}
