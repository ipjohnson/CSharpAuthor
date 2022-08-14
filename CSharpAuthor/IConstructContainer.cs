using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public interface IConstructContainer
    {
        ClassDefinition AddClass(string name);
    }
}
