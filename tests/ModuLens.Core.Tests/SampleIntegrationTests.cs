using ModuLens.Core.Documents;
using ModuLens.Core.Parsing;

namespace ModuLens.Core.Tests;

public sealed class SampleIntegrationTests
{
    [Fact]
    public void SampleUserscript_ProducesExpectedRepresentativeSectionsAndRanges()
    {
        var samplePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.js");
        var originalText = File.ReadAllText(samplePath);

        var document = new SectionParser().Parse(samplePath, originalText);

        Assert.Equal(21, document.Sections.Count);
        AssertSection(document, "Video detection", 212, 379, 212, 216, 217, 379);
        AssertSection(document, "RIGHT CLICK", 2079, 2230, 2079, 2100, 2101, 2230);
        AssertSection(document, "Start", 2757, 2774, 2757, 2761, 2762, 2774);
        Assert.Equal(originalText, document.Text);
        Assert.Equal(originalText, File.ReadAllText(samplePath));
    }

    private static void AssertSection(
        SourceDocument document,
        string name,
        int fullStart,
        int fullEnd,
        int headerStart,
        int headerEnd,
        int contentStart,
        int contentEnd)
    {
        var section = Assert.Single(document.Sections, candidate => candidate.Name == name);
        Assert.Equal((fullStart, fullEnd), (section.FullRange.StartLine, section.FullRange.EndLine));
        Assert.Equal((headerStart, headerEnd), (section.HeaderRange.StartLine, section.HeaderRange.EndLine));
        Assert.Equal((contentStart, contentEnd), (section.ContentRange.StartLine, section.ContentRange.EndLine));
    }
}
