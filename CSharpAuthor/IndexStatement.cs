namespace CSharpAuthor;

public class IndexStatement : WrapStatement{
    public IndexStatement(IOutputComponent instance, IOutputComponent index) :
        base(instance, CodeOutputComponent.Get(""), 
            new WrapStatement(index, "[","]")) { }

}