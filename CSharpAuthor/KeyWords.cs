namespace CSharpAuthor;

public static class KeyWords
{
    public static string Abstract => "abstract";

    public static string Async => "async";

    public static string Await => "await";
        
    public static string Class => "class";

    public static string Interface => "interface";
        
    public static string Override => "override";

    public static string Partial => "partial";

    public static string Private => "private";

    public static string Protected => "protected";
        
    public static string Public => "public";

    public static string ReadOnly => "readonly";

    public static string Static => "static";

    public static string Virtual => "virtual";

    public static string Internal => "internal";

    public static string Sealed => "sealed";

    public static string Record => "record";

    public static string Ref => "ref";

    public static string Out => "out";

    public static string In => "in";

    public static string Params => "params";

    public static string This => "this";

    /// <summary>
    /// <c>new</c> as a member modifier - hiding an inherited member, not object creation.
    /// </summary>
    public static string New => "new";

    /// <summary><c>const</c>.</summary>
    public static string Const => "const";

    /// <summary>
    /// <c>field</c> - the contextual keyword for a property's generated backing field. C# 14.
    /// </summary>
    public static string Field => "field";

    /// <summary>
    /// <c>file</c> - the accessibility level that confines a type to the file declaring it.
    /// </summary>
    public static string File => "file";
}