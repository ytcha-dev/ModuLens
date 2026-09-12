using ModuLens.Core.Documents;

namespace ModuLens.Core.Tests;

public sealed class SourceRangeTests
{
    [Fact]
    public void NonEmptyRange_UsesHalfOpenOffsetsAndInclusiveLines()
    {
        const string source = "alpha\nbeta";
        var range = new SourceRange(6, 10, 2, 2);

        Assert.False(range.IsEmpty);
        Assert.Equal(4, range.Length);
        Assert.Equal("beta", range.GetText(source));
        Assert.Equal(2, range.StartLine);
        Assert.Equal(2, range.EndLine);
    }

    [Fact]
    public void EmptyRange_UsesInsertionLineAndPreviousEndLine()
    {
        var range = new SourceRange(5, 5, 3, 2);

        Assert.True(range.IsEmpty);
        Assert.Equal(0, range.Length);
        Assert.Equal(string.Empty, range.GetText("12345"));
    }

    [Fact]
    public void EmptyRange_WithInclusiveLineSpan_IsRejected()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new SourceRange(5, 5, 3, 3));

        Assert.Equal("endLine", exception.ParamName);
    }
}
