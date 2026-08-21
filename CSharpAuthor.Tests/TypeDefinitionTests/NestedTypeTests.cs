using System.Collections.Generic;
using System.Linq;
using System.Text;
using NestingSample;
using Xunit;

namespace CSharpAuthor.Tests.TypeDefinitionTests
{
    /// <summary>
    /// A nested type is named through the type that declares it. Dropping the container does not
    /// produce a shorter name for the same type - it produces the name of a different type, or of no
    /// type at all, and the compiler is the first thing to find out.
    /// </summary>
    public class NestedTypeTests
    {
        private const string Ns = "NestingSample";

        [Theory]
        [InlineData(TypeOutputMode.ShortName, "Outer.Inner")]
        [InlineData(TypeOutputMode.FullName, Ns + ".Outer.Inner")]
        [InlineData(TypeOutputMode.Global, "global::" + Ns + ".Outer.Inner")]
        public void OneLevelOfNesting(TypeOutputMode mode, string expected)
        {
            var builder = new StringBuilder();

            TypeDefinition.Get(typeof(Outer.Inner)).WriteTypeName(builder, mode);

            Assert.Equal(expected, builder.ToString());
        }

        [Theory]
        [InlineData(TypeOutputMode.ShortName, "Outer.Inner.Deepest")]
        [InlineData(TypeOutputMode.FullName, Ns + ".Outer.Inner.Deepest")]
        [InlineData(TypeOutputMode.Global, "global::" + Ns + ".Outer.Inner.Deepest")]
        public void TwoLevelsOfNesting(TypeOutputMode mode, string expected)
        {
            var builder = new StringBuilder();

            TypeDefinition.Get(typeof(Outer.Inner.Deepest)).WriteTypeName(builder, mode);

            Assert.Equal(expected, builder.ToString());
        }

        /// <summary>
        /// The qualification belongs to the outermost type only: <c>global::</c> is written once, at the
        /// front, and never again part way down the chain.
        /// </summary>
        [Theory]
        [InlineData(TypeOutputMode.ShortName, "OuterGeneric<int>.Inner<string>")]
        [InlineData(TypeOutputMode.FullName, Ns + ".OuterGeneric<int>.Inner<string>")]
        [InlineData(TypeOutputMode.Global, "global::" + Ns + ".OuterGeneric<int>.Inner<string>")]
        public void GenericInsideGeneric(TypeOutputMode mode, string expected)
        {
            var builder = new StringBuilder();

            TypeDefinition.Get(typeof(OuterGeneric<int>.Inner<string>)).WriteTypeName(builder, mode);

            Assert.Equal(expected, builder.ToString());
        }

        /// <summary>
        /// Reflection hangs the container's type arguments off the nested type, so a type with no
        /// parameters of its own looks generic. Writing those arguments here invents a
        /// <c>Plain&lt;int&gt;</c> that was never declared.
        /// </summary>
        [Theory]
        [InlineData(TypeOutputMode.ShortName, "OuterGeneric<int>.Plain")]
        [InlineData(TypeOutputMode.Global, "global::" + Ns + ".OuterGeneric<int>.Plain")]
        public void NonGenericInsideGeneric(TypeOutputMode mode, string expected)
        {
            var builder = new StringBuilder();

            TypeDefinition.Get(typeof(OuterGeneric<int>.Plain)).WriteTypeName(builder, mode);

            Assert.Equal(expected, builder.ToString());
        }

        [Theory]
        [InlineData(TypeOutputMode.ShortName, "Outer.GenericInner<string>")]
        [InlineData(TypeOutputMode.Global, "global::" + Ns + ".Outer.GenericInner<string>")]
        public void GenericInsideNonGeneric(TypeOutputMode mode, string expected)
        {
            var builder = new StringBuilder();

            TypeDefinition.Get(typeof(Outer.GenericInner<string>)).WriteTypeName(builder, mode);

            Assert.Equal(expected, builder.ToString());
        }

        [Fact]
        public void ThreeLevelsOfGenericNesting()
        {
            Assert.Equal(
                "OuterGeneric<int>.Inner<string>.Deepest",
                TypeDefinition.Get(typeof(OuterGeneric<int>.Inner<string>.Deepest)).GetShortName());
        }

        /// <summary>
        /// <see cref="ITypeDefinition.Name"/> and <see cref="ITypeDefinition.Namespace"/> keep the
        /// meaning reflection gives them; the container is a separate value, so the namespace is still
        /// the one to import.
        /// </summary>
        [Fact]
        public void TheContainerIsHeldSeparatelyFromTheName()
        {
            var inner = TypeDefinition.Get(typeof(Outer.Inner));

            Assert.Equal("Inner", inner.Name);
            Assert.Equal(Ns, inner.Namespace);

            Assert.NotNull(inner.ContainingType);
            Assert.Equal("Outer", inner.ContainingType!.Name);
            Assert.Equal(Ns, inner.ContainingType.Namespace);
            Assert.Null(inner.ContainingType.ContainingType);
        }

        [Fact]
        public void ATypeInANamespaceHasNoContainer()
        {
            Assert.Null(TypeDefinition.Get(typeof(Outer)).ContainingType);
            Assert.Null(TypeDefinition.Get(typeof(int)).ContainingType);
            Assert.Null(TypeDefinition.Get(typeof(List<int>)).ContainingType);
            Assert.Null(new TypeParameterDefinition("T").ContainingType);
        }

        /// <summary>
        /// Short names work only if the namespace reaches the using list, and a nested type's namespace
        /// is its outermost container's.
        /// </summary>
        [Fact]
        public void TheNamespaceIsStillTheOneToImport()
        {
            Assert.Contains(Ns, TypeDefinition.Get(typeof(Outer.Inner)).KnownNamespaces);
            Assert.Contains(Ns, TypeDefinition.Get(typeof(Outer.Inner.Deepest)).KnownNamespaces);
            Assert.Contains(Ns, TypeDefinition.Get(typeof(OuterGeneric<int>.Inner<string>)).KnownNamespaces);

            Assert.Contains(
                "System.Collections.Generic",
                TypeDefinition.Get(typeof(OuterGeneric<List<int>>.Plain)).KnownNamespaces);
        }

        /// <summary>
        /// End to end: the file that writes a nested type in short-name mode gets the one using it
        /// needs and the container it needs, and neither is guessed at by a writer.
        /// </summary>
        [Fact]
        public void WritesThroughTheOutputContext()
        {
            var context = new OutputContext();

            context.Write(TypeDefinition.Get(typeof(Outer.Inner)));
            context.GenerateUsingStatements();

            Assert.Equal("using " + Ns + ";\n\nOuter.Inner", context.Output());
        }

        [Fact]
        public void GlobalModeWritesNoUsingAndKeepsTheContainer()
        {
            var context = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

            context.Write(TypeDefinition.Get(typeof(Outer.Inner)));
            context.GenerateUsingStatements();

            Assert.Equal("global::" + Ns + ".Outer.Inner", context.Output());
        }

        [Fact]
        public void NestedTypesTakeArrayShapesAndNullability()
        {
            Assert.Equal("Outer.Inner[]", TypeDefinition.Get(typeof(Outer.Inner[])).GetShortName());
            Assert.Equal("Outer.Inner[][]", TypeDefinition.Get(typeof(Outer.Inner[][])).GetShortName());
            Assert.Equal("Outer.Inner[,]", TypeDefinition.Get(typeof(Outer.Inner[,])).GetShortName());
            Assert.Equal("Outer.Inner?", TypeDefinition.Get(typeof(Outer.Inner)).MakeNullable().GetShortName());

            Assert.Equal(
                "global::" + Ns + ".Outer.Inner[][]",
                TypeDefinition.Get(typeof(Outer.Inner)).MakeArray().MakeArray().GetShortNameIn(TypeOutputMode.Global));
        }

        [Fact]
        public void ANestedTypeIsUsableAsATypeArgument()
        {
            Assert.Equal(
                "List<Outer.Inner>",
                TypeDefinition.Get(typeof(List<Outer.Inner>)).GetShortName());

            Assert.Equal(
                "global::System.Collections.Generic.List<global::" + Ns + ".Outer.Inner>",
                TypeDefinition.Get(typeof(List<Outer.Inner>)).GetShortNameIn(TypeOutputMode.Global));
        }

        [Fact]
        public void TheKindOfANestedTypeSurvives()
        {
            Assert.Equal(TypeDefinitionEnum.InterfaceDefinition, TypeDefinition.Get(typeof(IOuter.IInner)).TypeDefinitionEnum);
            Assert.Equal(TypeDefinitionEnum.EnumDefinition, TypeDefinition.Get(typeof(Outer.Kind)).TypeDefinitionEnum);
            Assert.Equal("Outer.Kind", TypeDefinition.Get(typeof(Outer.Kind)).GetShortName());
            Assert.Equal("IOuter.IInner", TypeDefinition.Get(typeof(IOuter.IInner)).GetShortName());
        }

        /// <summary>
        /// Two nested types with the same name in different containers used to compare equal, which is
        /// what a dictionary keyed on a type definition would have collided on.
        /// </summary>
        [Fact]
        public void TheContainerIsPartOfTheValue()
        {
            var fromOuter = TypeDefinition.Get(typeof(Outer.Inner));
            var fromOther = TypeDefinition.Get(typeof(Other.Inner));

            Assert.NotEqual(fromOuter, fromOther);
            Assert.NotEqual(0, fromOuter.CompareTo(fromOther));
            Assert.NotEqual(fromOuter.GetHashCode(), fromOther.GetHashCode());

            Assert.Equal(fromOuter, TypeDefinition.Get(typeof(Outer.Inner)));
            Assert.Equal(fromOuter.GetHashCode(), TypeDefinition.Get(typeof(Outer.Inner)).GetHashCode());

            Assert.NotEqual(fromOuter, TypeDefinition.Get(typeof(Outer)));
        }

        [Fact]
        public void ANestedTypeCanBeBuiltByHand()
        {
            var outer = TypeDefinition.Get(Ns, "Outer");
            var inner = TypeDefinition.GetNested(outer, "Inner");

            Assert.Equal("Outer.Inner", inner.GetShortName());
            Assert.Equal("global::" + Ns + ".Outer.Inner", inner.GetShortNameIn(TypeOutputMode.Global));
            Assert.Equal(TypeDefinition.Get(typeof(Outer.Inner)), inner);

            var deepest = TypeDefinition.GetNested(inner, "Deepest");

            Assert.Equal("Outer.Inner.Deepest", deepest.GetShortName());
        }

        /// <summary>
        /// A generic container keeps its arguments unrendered until the whole chain is written, so one
        /// output mode flips the container and its arguments together.
        /// </summary>
        [Fact]
        public void AHandBuiltGenericContainerDefersItsArguments()
        {
            var container = new GenericTypeDefinition(
                TypeDefinitionEnum.ClassDefinition,
                Ns,
                "OuterGeneric",
                new[] { TypeDefinition.Get(typeof(List<int>)) });

            var nested = TypeDefinition.GetNested(container, "Plain");

            Assert.Equal("OuterGeneric<List<int>>.Plain", nested.GetShortName());
            Assert.Equal(
                "global::" + Ns + ".OuterGeneric<global::System.Collections.Generic.List<int>>.Plain",
                nested.GetShortNameIn(TypeOutputMode.Global));
        }

        /// <summary>
        /// A generic parameter is declared by a type but not nested in it: <c>T</c> is <c>T</c>, not
        /// <c>List.T</c>.
        /// </summary>
        [Fact]
        public void AGenericParameterIsNotANestedType()
        {
            var parameter = TypeDefinition.Get(typeof(List<>).GetGenericArguments().First());

            Assert.Equal("T", parameter.GetShortName());
            Assert.Equal("T", parameter.GetShortNameIn(TypeOutputMode.Global));
            Assert.Null(parameter.ContainingType);
        }
    }

    internal static class TypeOutputModeExtensions
    {
        public static string GetShortNameIn(this ITypeDefinition typeDefinition, TypeOutputMode mode)
        {
            var builder = new StringBuilder();

            typeDefinition.WriteTypeName(builder, mode);

            return builder.ToString();
        }
    }
}

namespace NestingSample
{
    public class Outer
    {
        public class Inner
        {
            public class Deepest
            {
            }
        }

        public class GenericInner<T>
        {
        }

        public enum Kind
        {
            One
        }
    }

    public class Other
    {
        public class Inner
        {
        }
    }

    public class OuterGeneric<T>
    {
        public class Inner<TValue>
        {
            public class Deepest
            {
            }
        }

        public class Plain
        {
        }
    }

    public interface IOuter
    {
        public interface IInner
        {
        }
    }
}
