using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpAuthor
{
    [Flags]
    public enum ComponentModifier
    {
        None = 0,

        Public = 1,

        Protected = 2,

        Private = 4,

        Readonly = 8,

        Static = 16,

        Virtual = 32,

        Override = 64,

        Abstract = 128,
    }
}
