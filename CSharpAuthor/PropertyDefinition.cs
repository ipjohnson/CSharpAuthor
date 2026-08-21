using System.Collections.Generic;
using CSharpAuthor.Profiles;

namespace CSharpAuthor;

/// <summary>
/// A property or an indexer, and its accessors.
/// </summary>
/// <remarks>
/// <para>
/// The shape is worked out from what was set rather than declared, so the common case needs no
/// call at all. An auto-property is a property whose accessors have no statements; giving
/// <see cref="Get"/> statements produces a full accessor block; setting
/// <see cref="Set"/> to null produces <c>{ get; }</c>.
/// </para>
/// <example>
/// <code>
/// greeter.AddProperty(typeof(string), "Name");
/// // public string Name { get; set; }
///
/// greeter.AddProperty(typeof(string), "Name").Set = null;
/// // public string Name { get; }
///
/// greeter.AddProperty(typeof(string), "Name").Set.IsInit = true;
/// // public string Name { get; init; }
///
/// var count = greeter.AddProperty(typeof(int), "Count");
/// count.DefaultValue = new CodeOutputComponent("0") { Indented = false };
/// // public int Count { get; set; } = 0;
/// </code>
/// </example>
/// <para>
/// <strong>A property named <c>this</c> is an indexer.</strong> That is a magic string with no
/// other signal: nothing in the type, the constructor or the signature says so, and it is the one
/// property whose name is not escaped as an identifier - see <see cref="Name"/> and
/// <see cref="IndexType"/>.
/// </para>
/// </remarks>
public class PropertyDefinition : BaseOutputComponent, INamedComponent
{
    /// <summary>
    /// A property of <paramref name="type"/> named <paramref name="name"/>. Prefer
    /// <see cref="ClassDefinition.AddProperty(ITypeDefinition, string)"/>, which builds one and
    /// attaches it to a type.
    /// </summary>
    /// <remarks>
    /// Both accessors exist from the start, which is what makes <c>{ get; set; }</c> the default -
    /// a get-only property is one whose <see cref="Set"/> was removed, not one that never had it.
    /// </remarks>
    public PropertyDefinition(ITypeDefinition type, string name)
    {
        Name = name;
        Type = type;

        Get = new PropertyMethodDefinition();
        Set = new PropertyMethodDefinition();
    }

    /// <summary>
    /// The declared name, escaped with <c>@</c> if it is a keyword - <em>except</em> when it is
    /// <c>this</c>, which declares an indexer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the trap. A property named <c>this</c> that also declares an index writes
    /// <c>public string this[int index]</c>; it is the one name in this library treated as a C#
    /// keyword rather than as an identifier, and the only thing marking it as one is the string.
    /// </para>
    /// <para>
    /// Both halves have to line up, and neither is checked:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// An index but a name other than <c>this</c> - <c>"Item"</c> is the usual guess, because that
    /// is what the CLR calls an indexer - emits <c>public string Item[int index]</c>, which is not
    /// a declaration C# has (CS1519).
    /// </description></item>
    /// <item><description>
    /// The name <c>this</c> with no index is not an indexer at all: it is escaped like any other
    /// keyword and emits <c>public string @this { get; set; }</c>.
    /// </description></item>
    /// </list>
    /// <para>
    /// A change to how identifiers are escaped is therefore a change to every indexer in every
    /// consumer, which is exactly how one broke.
    /// </para>
    /// </remarks>
    public string Name { get; }

    /// <summary>The property's type - the type of the value, not of the index.</summary>
    public ITypeDefinition Type { get; }

    /// <summary>
    /// The getter. Always present; it is having statements that turns it from
    /// <c>{ get; }</c> into a block.
    /// </summary>
    /// <remarks>
    /// <example>
    /// <code>
    /// var name = greeter.AddProperty(typeof(string), "Name");
    /// name.Set = null;
    /// name.Get.LambdaSyntax = true;
    /// name.Get.AddCode("_name");
    /// // public string Name => _name;
    /// </code>
    /// Without <see cref="PropertyMethodDefinition.LambdaSyntax"/> the same statements give the
    /// braced form:
    /// <code>
    /// public string Name
    /// {
    ///     get
    ///     {
    ///         return _name;
    ///     }
    /// }
    /// </code>
    /// </example>
    /// It is a <see cref="MethodDefinition"/>, so the body is built with the same statement methods
    /// a method body uses.
    /// </remarks>
    public PropertyMethodDefinition Get { get; }

    /// <summary>
    /// The setter, or null for a property with no setter at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Set it to null for <c>{ get; }</c>. Leave it in place and set
    /// <see cref="PropertyMethodDefinition.IsInit"/> for <c>{ get; init; }</c>, or
    /// <see cref="BaseOutputComponent.Modifiers"/> on it for <c>{ get; private set; }</c> - the
    /// accessibility of an accessor is set on the accessor, not on the property.
    /// </para>
    /// <para>
    /// Null is not the same as an empty setter: an empty one still writes <c>set;</c>. It is the
    /// difference between a property that cannot be assigned and one that can be assigned and does
    /// nothing.
    /// </para>
    /// </remarks>
    public PropertyMethodDefinition? Set { get; set; }

    /// <summary>
    /// The type of a single index, for the common <c>this[int index]</c> shape. Ignored when
    /// <see cref="IndexParameters"/> has entries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Setting this is half of declaring an indexer; the other half is naming the property
    /// <c>this</c>. Neither half checks the other.
    /// </para>
    /// <example>
    /// <code>
    /// var item = bag.AddProperty(typeof(string), "this");
    /// item.IndexType = TypeDefinition.Get(typeof(int));
    /// item.Get.Return("_items[index]");
    /// item.Set.AddIndentedStatement("_items[index] = value");
    /// </code>
    /// which is
    /// <code>
    /// public string this[int index]
    /// {
    ///     get
    ///     {
    ///         return _items[index];
    ///     }
    ///     set
    ///     {
    ///         _items[index] = value;
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <para>
    /// An indexer has no auto-property and no expression-bodied form, so it always writes its
    /// accessors out in full - including when <see cref="Get"/> has no statements, which produces
    /// an accessor with an empty body rather than <c>{ get; }</c>.
    /// </para>
    /// </remarks>
    public ITypeDefinition? IndexType { get; set; }

    /// <summary>
    /// The name of the single index declared through <see cref="IndexType"/>.
    /// </summary>
    /// <remarks>
    /// This is the name the accessor bodies refer to, so changing it means changing the statements
    /// written into <see cref="Get"/> and <see cref="Set"/> to match.
    /// </remarks>
    public string IndexName { get; set; } = "index";

    /// <summary>
    /// Indices for an indexer that takes more than one, such as <c>this[int row, int column]</c>.
    /// Takes precedence over <see cref="IndexType"/>.
    /// </summary>
    /// <remarks>
    /// Precedence rather than combination: with even one entry here, <see cref="IndexType"/> and
    /// <see cref="IndexName"/> are not written at all. Setting both is a request that cannot be
    /// honoured, so it is resolved rather than merged into a signature nobody asked for.
    /// </remarks>
    public List<ParameterDefinition> IndexParameters { get; } = new();

    /// <summary>
    /// Adds one index to a multi-index indexer, returning the property so calls chain.
    /// </summary>
    /// <remarks>
    /// <example>
    /// <code>
    /// var cell = grid.AddProperty(typeof(int), "this");
    /// cell.AddIndexParameter(TypeDefinition.Get(typeof(int)), "row")
    ///     .AddIndexParameter(TypeDefinition.Get(typeof(int)), "column");
    /// </code>
    /// which is <c>public int this[int row, int column]</c>.
    /// </example>
    /// Use <see cref="IndexType"/> for the single-index case; it is one assignment rather than a
    /// call, and it names the index <c>index</c> for you.
    /// </remarks>
    public PropertyDefinition AddIndexParameter(ITypeDefinition type, string name)
    {
        IndexParameters.Add(new ParameterDefinition(type, name));

        return this;
    }

    private bool IsIndexer => IndexParameters.Count > 0 || IndexType != null;

    /// <summary>
    /// The property used as a value expression - its own name - for building statements that read
    /// or assign it.
    /// </summary>
    /// <remarks>
    /// A fresh instance each time, so it cannot be used to carry state; it is a name, not a
    /// reference to this declaration.
    /// </remarks>
    public InstanceDefinition Instance => new(Name);

    /// <summary>
    /// The initialiser: <c>public int Count { get; set; } = 0;</c>.
    /// </summary>
    /// <remarks>
    /// Only written on an auto-property - one whose accessors have no statements and which is not
    /// an indexer. A property with an accessor body has nowhere to put an initialiser, so this is
    /// silently not written rather than emitted somewhere invalid.
    /// </remarks>
    public IOutputComponent? DefaultValue { get; set; }

    /// <summary>
    /// Whether the property is <c>required</c>: the caller has to set it, and the compiler checks.
    /// </summary>
    /// <remarks>
    /// C# 11, and polyfillable - see <see cref="LanguageFeature.RequiredMembers"/>. Below it the
    /// keyword is dropped and a <c>// DOWNLEVEL:</c> comment says that nothing is enforcing the
    /// initialisation any more, because a property that silently stops being required is exactly
    /// the kind of change nobody notices until something is null in production.
    /// </remarks>
    public bool IsRequired { get; set; }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        var session = outputContext.EmitSession();

        // Asked before anything for this member is written: a `// DOWNLEVEL:` comment is a line
        // of its own and cannot be inserted into a half-written one. With no profile in force the
        // session answers yes to both, which is what V1 did.
        var writeInit = Set is { IsInit: true } &&
                        session.MayEmit(LanguageFeature.InitOnlyProperties, outputContext, Name);

        var writeRequired = IsRequired &&
                            session.MayEmit(LanguageFeature.RequiredMembers, outputContext, Name);

        WriteAccessModifiers(outputContext);

        if (writeRequired)
        {
            outputContext.Write("required ");
        }

        outputContext.Write(Type);
        outputContext.WriteSpace();
        // An indexer is declared as `this[...]`, where `this` is the keyword and not a name, so it
        // is the one property whose name must not be escaped.
        //
        // It is also the one property whose name is not the caller's to choose - named anything
        // else this emits `public int Item[string index]`, which is not a declaration C# has
        // (CS1519, adversary #51). Writing `this` there is blocked by an original test,
        // PropertyDefinitionTests.SimplePropertyDefinitionTests.IndexedGetSetDefinition, which
        // asserts the invalid form character for character. See docs/migration-v1-v2.md.
        outputContext.Write(IsIndexer ? Name : CSharpIdentifier.Escape(Name));

        if (IndexParameters.Count > 0)
        {
            outputContext.Write("[");

            for (var i = 0; i < IndexParameters.Count; i++)
            {
                if (i > 0)
                {
                    outputContext.Write(", ");
                }

                IndexParameters[i].WriteWithSignature(outputContext);
            }

            outputContext.Write("]");
        }
        else if (IndexType != null)
        {
            outputContext.Write("[");
            outputContext.Write(IndexType);
            outputContext.Write(" ");
            outputContext.Write(CSharpIdentifier.Escape(IndexName));
            outputContext.Write("]");
        }

        // An indexer has no auto-property or expression-bodied form to fall back to, so it always
        // writes its accessors out in full.
        if (Set == null && !IsIndexer)
        {
            if (Get.StatementCount == 0)
            {
                outputContext.WriteLine(" { get; }");
                return;
            }
                
            if (Get.LambdaSyntax)
            {
                Get.WriteOutput(outputContext);
                return;
            }
        }
        else if (!IsIndexer &&
                 Get.StatementCount == 0 &&
                 Set is { StatementCount: 0 })
        {
            // writeInit, not Set.IsInit: below C#9 `init` downlevels to `set`.
            var setterAccess = Set.Modifiers.GetAccessorAccessibilityKeywords();

            if (string.IsNullOrEmpty(setterAccess))
            {
                // The whole accessor list as one constant. It is what nearly every auto-property
                // writes, and building it out of pieces made a string per property for no reason.
                outputContext.Write(writeInit ? " { get; init; }" : " { get; set; }");
            }
            else
            {
                outputContext.Write(" { get; ");
                outputContext.Write(setterAccess);
                outputContext.WriteSpace();
                outputContext.Write(writeInit ? "init" : "set");
                outputContext.Write("; }");
            }

            if (DefaultValue != null)
            {
                outputContext.Write(" = ");
                DefaultValue.WriteOutput(outputContext);
                outputContext.Write(";");
            }
            
            outputContext.WriteLine();
            
            return;
        }

        outputContext.WriteLine();
        outputContext.OpenScope();

        outputContext.WriteIndent("get");
        Get.WriteOutput(outputContext);

        if (Set != null)
        {
            outputContext.WriteIndent();

            var setterAccess = Set.Modifiers.GetAccessorAccessibilityKeywords();

            if (!string.IsNullOrEmpty(setterAccess))
            {
                outputContext.Write(setterAccess);
                outputContext.WriteSpace();
            }

            outputContext.Write(Set.IsInit ? "init" : "set");
            Set.WriteOutput(outputContext);
        }

        outputContext.CloseScope();
    }

    protected override void WriteComment(IOutputContext outputContext)
    {
        if (string.IsNullOrWhiteSpace(Comment))
        {
            return;
        }
        
        DocumentationComment.WriteSummary(outputContext.WriteIndentedLine, Comment);
    }

    protected virtual void WriteAccessModifiers(IOutputContext outputContext)
    {
        var modifier = GetAccessModifier("public");

        outputContext.WriteIndent(modifier);

        // The space belongs to the keyword, so no keyword means no space. Writing it
        // unconditionally is cheaper by one concat and leaves ` int P { get; set; }` when the
        // accessibility is NoAccessibility - which ModifierAdversaryTests pins.
        if (modifier.Length > 0)
        {
            outputContext.WriteSpace();
        }

        outputContext.Write(
            Modifiers.GetModifierKeywords(ComponentModifierExtensions.PropertyModifiers));
    }
}