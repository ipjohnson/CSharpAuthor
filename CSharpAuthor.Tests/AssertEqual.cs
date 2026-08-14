using Xunit;

namespace CSharpAuthor.Tests;

internal static class AssertEqual
{
    public static void WithoutNewLine(string expected, string actual)
    {
        Assert.Equal(expected.Replace("\r\n","\n"), actual.Replace("\r\n","\n"));
    }

    /// <summary>
    /// <c>Assert.Contains</c> with line endings normalised on both sides.
    /// </summary>
    /// <remarks>
    /// A multi-line expectation cannot be compared verbatim. String literals in a test file take
    /// their line endings from the checkout - CRLF on Windows - while the writer emits its own, so
    /// an assertion that passes on one platform fails on the other, and the failure message prints
    /// the two as identical. That is what this and <see cref="WithoutNewLine"/> exist to avoid.
    /// </remarks>
    public static void ContainsWithoutNewLine(string expected, string actual)
    {
        Assert.Contains(expected.Replace("\r\n","\n"), actual.Replace("\r\n","\n"));
    }
}