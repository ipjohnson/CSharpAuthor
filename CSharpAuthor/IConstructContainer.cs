using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public interface IConstructContainer
{
    IEnumerable<IOutputComponent> GetAllNamedComponents();
    
    ClassDefinition AddClass(string name);
    
    InterfaceDefinition AddInterface(string name);
    
    EnumDefinition AddEnum(string name);
}