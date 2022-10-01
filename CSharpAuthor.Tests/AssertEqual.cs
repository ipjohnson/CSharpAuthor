using Xunit;

namespace CSharpAuthor.Tests;

internal static class AssertEqual
{
    public static void WithoutNewLine(string expected, string actual)
    {
        Assert.Equal(expected.Replace("\r\n","\n"), actual.Replace("\r\n","\n"));
    }
}