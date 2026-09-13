using ModuLens.Core.Editing;
using ModuLens.Core.Parsing;

namespace ModuLens.Core.Tests;

public sealed class SourceDocumentEditorTests
{
    [Fact]
    public void ReplaceSectionContent_PreservesUnrelatedTextAndRecalculatesRanges()
    {
        var source = Lines(
            "// preamble",
            "/**",
            " * ===",
            " * First",
            " * ===",
            " */",
            "const first = 1;",
            "/**",
            " * ===",
            " * Second",
            " * ===",
            " */",
            "const second = 2;");
        var parser = new SectionParser();
        var original = parser.Parse("fixture.js", source);
        var editor = new SourceDocumentEditor(parser);
        var replacement = Lines(
            "const first = 10;",
            "const extra = 20;") + "\n";

        var updated = editor.ReplaceSectionContent(original, 0, replacement);

        var first = updated.Sections[0];
        var second = updated.Sections[1];
        Assert.Equal(2, updated.Sections.Count);
        Assert.Equal("// preamble\n", updated.Text[..first.FullRange.StartOffset]);
        Assert.Equal(replacement, first.ContentRange.GetText(updated.Text));
        Assert.Equal("const second = 2;", second.ContentRange.GetText(updated.Text));
        Assert.Equal(7, first.ContentRange.StartLine);
        Assert.Equal(8, first.ContentRange.EndLine);
        Assert.Equal(9, second.FullRange.StartLine);
        Assert.Equal(14, second.FullRange.EndLine);
        Assert.Equal(source[..original.Sections[0].ContentRange.StartOffset],
            updated.Text[..first.ContentRange.StartOffset]);
        Assert.Equal(source[original.Sections[0].ContentRange.EndOffset..],
            updated.Text[(first.ContentRange.StartOffset + replacement.Length)..]);
    }

    [Fact]
    public void ReplaceSectionContent_CanPopulateAnEmptySection()
    {
        var source = Lines(
            "/**",
            " * ===",
            " * Empty",
            " * ===",
            " */",
            "/**",
            " * ===",
            " * Next",
            " * ===",
            " */",
            "next();");
        var parser = new SectionParser();
        var document = parser.Parse("fixture.js", source);

        var updated = new SourceDocumentEditor(parser)
            .ReplaceSectionContent(document, 0, "emptyNowHasCode();\n");

        Assert.Equal("emptyNowHasCode();\n", updated.Sections[0].ContentRange.GetText(updated.Text));
        Assert.Equal(2, updated.Sections.Count);
        Assert.Equal("next();", updated.Sections[1].ContentRange.GetText(updated.Text));
    }

    [Fact]
    public void ReplaceSectionContent_RejectsAStaleSectionIndex()
    {
        var document = new SectionParser().Parse("plain.js", "const value = 1;");

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SourceDocumentEditor().ReplaceSectionContent(document, 0, "replacement"));

        Assert.Equal("sectionIndex", exception.ParamName);
    }

    private static string Lines(params string[] lines) => string.Join('\n', lines);
}
