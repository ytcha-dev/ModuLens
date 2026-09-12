using ModuLens.Core.Documents;
using ModuLens.Core.Parsing;

namespace ModuLens.Core.Tests;

public sealed class SectionParserTests
{
    private readonly SectionParser parser = new();

    [Fact]
    public void SimpleHeading_ProducesFullHeaderAndContentRanges()
    {
        var source = Lines(
            "preamble",
            "/**",
            " * ===",
            " * Simple",
            " * ===",
            " */",
            "",
            "const value = 1;");

        var document = parser.Parse("simple.js", source);

        var section = Assert.Single(document.Sections);
        Assert.Equal("Simple", section.Name);
        AssertRange(section.FullRange, 2, 8, false);
        AssertRange(section.HeaderRange, 2, 6, false);
        AssertRange(section.ContentRange, 7, 8, false);
        Assert.Equal(source, document.Text);
        Assert.StartsWith("/**\n * ===", section.HeaderRange.GetText(source));
        Assert.Equal("\nconst value = 1;", section.ContentRange.GetText(source));
    }

    [Fact]
    public void HeadingWithDescription_IncludesDescriptionInHeaderRange()
    {
        var source = Lines(
            "/**",
            " * =====",
            " * RIGHT CLICK",
            " *",
            " * Explanation.",
            " * =====",
            " */",
            "",
            "installHandler();");

        var document = parser.Parse("described.js", source);

        var section = Assert.Single(document.Sections);
        Assert.Equal("RIGHT CLICK", section.Name);
        AssertRange(section.FullRange, 1, 9, false);
        AssertRange(section.HeaderRange, 1, 7, false);
        AssertRange(section.ContentRange, 8, 9, false);
        Assert.Contains("Explanation.", section.HeaderRange.GetText(source));
        Assert.DoesNotContain("installHandler", section.HeaderRange.GetText(source));
    }

    [Fact]
    public void MultipleSections_EndAtTheLineBeforeTheNextHeading()
    {
        var source = Lines(
            "/**",
            " * ===",
            " * First",
            " * ===",
            " */",
            "first();",
            "/**",
            " * ===",
            " * Second",
            " * ===",
            " */",
            "second();");

        var document = parser.Parse("multiple.js", source);

        Assert.Collection(
            document.Sections,
            first =>
            {
                Assert.Equal("First", first.Name);
                AssertRange(first.FullRange, 1, 6, false);
                AssertRange(first.ContentRange, 6, 6, false);
            },
            second =>
            {
                Assert.Equal("Second", second.Name);
                AssertRange(second.FullRange, 7, 12, false);
                AssertRange(second.ContentRange, 12, 12, false);
            });
    }

    [Fact]
    public void FinalSection_ExtendsToEndOfFileWithoutTrailingNewline()
    {
        var source = Lines(
            "/**",
            " * ===",
            " * Final",
            " * ===",
            " */",
            "finish();");

        var section = Assert.Single(parser.Parse("final.js", source).Sections);

        Assert.Equal(source.Length, section.FullRange.EndOffset);
        AssertRange(section.FullRange, 1, 6, false);
        Assert.Equal("finish();", section.ContentRange.GetText(source));
    }

    [Fact]
    public void MalformedHeading_IsIgnoredAndParsingContinues()
    {
        var source = Lines(
            "/**",
            " * ===",
            " * Broken",
            " * not a closing separator",
            " */",
            "/**",
            " * ===",
            " * Valid",
            " * ===",
            " */",
            "valid();");

        var section = Assert.Single(parser.Parse("malformed.js", source).Sections);

        Assert.Equal("Valid", section.Name);
        Assert.Equal(6, section.FullRange.StartLine);
    }

    [Fact]
    public void AdjacentHeadings_ProduceAnEmptyContentRange()
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
            " */");

        var document = parser.Parse("empty.js", source);

        var empty = document.Sections[0];
        AssertRange(empty.FullRange, 1, 5, false);
        AssertRange(empty.HeaderRange, 1, 5, false);
        AssertRange(empty.ContentRange, 6, 5, true);
        Assert.Equal(string.Empty, empty.ContentRange.GetText(source));
    }

    [Fact]
    public void BlankLineBetweenHeadings_IsPreservedAsNearEmptyContent()
    {
        var source = Lines(
            "/**",
            " * ===",
            " * Near empty",
            " * ===",
            " */",
            "",
            "/**",
            " * ===",
            " * Next",
            " * ===",
            " */");

        var section = parser.Parse("near-empty.js", source).Sections[0];

        AssertRange(section.ContentRange, 6, 6, false);
        Assert.Equal("\n", section.ContentRange.GetText(source));
    }

    [Fact]
    public void FileWithoutHeadings_ProducesAnEmptySectionCollection()
    {
        const string source = "const value = 1;";

        var document = parser.Parse("plain.js", source);

        Assert.Empty(document.Sections);
        Assert.Equal(source, document.Text);
    }

    [Fact]
    public void CrLfInput_IsPreservedWithoutNormalization()
    {
        var source = string.Join(
            "\r\n",
            "/**",
            " * ===",
            " * Windows lines",
            " * ===",
            " */",
            "run();");

        var document = parser.Parse("windows.js", source);

        var section = Assert.Single(document.Sections);
        Assert.Equal(source, document.Text);
        Assert.Equal("run();", section.ContentRange.GetText(document.Text));
        AssertRange(section.FullRange, 1, 6, false);
    }

    private static string Lines(params string[] lines) => string.Join('\n', lines);

    private static void AssertRange(
        SourceRange range,
        int startLine,
        int endLine,
        bool isEmpty)
    {
        Assert.Equal(startLine, range.StartLine);
        Assert.Equal(endLine, range.EndLine);
        Assert.Equal(isEmpty, range.IsEmpty);
    }
}
