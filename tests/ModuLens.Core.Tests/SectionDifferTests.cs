using ModuLens.Core.Diff;
using ModuLens.Core.Editing;
using ModuLens.Core.Git;
using ModuLens.Core.Parsing;

namespace ModuLens.Core.Tests;

public sealed class SectionDifferTests
{
    [Fact]
    public void Compare_UnchangedSectionAlignsFullRangeWithAbsoluteLineNumbers()
    {
        var source = "// preamble\n" + Section("Stable", "stable();\n");
        var head = Parse(source);
        var working = Parse(source);
        var change = Assert.Single(new SectionComparer().Compare(head, working));

        var diff = new SectionDiffer().Compare(change, head, working);

        Assert.Equal(SectionChangeKind.Unchanged, diff.ChangeKind);
        Assert.Equal(6, diff.Lines.Count);
        Assert.All(diff.Lines, line => Assert.Equal(SectionDiffLineKind.Unchanged, line.Kind));
        Assert.Equal(2, diff.Lines[0].HeadLineNumber);
        Assert.Equal(2, diff.Lines[0].WorkingLineNumber);
        Assert.Equal("/**", diff.Lines[0].HeadText);
        Assert.Equal("stable();", diff.Lines[^1].WorkingText);
    }

    [Fact]
    public void Compare_ReplacementPairsRemovedAndAddedLinesAsModified()
    {
        var head = Parse(Section("Change", "before();\nkeep();\n"));
        var working = Parse(Section("Change", "after();\nkeep();\n"));
        var change = Assert.Single(new SectionComparer().Compare(head, working));

        var diff = new SectionDiffer().Compare(change, head, working);
        var modified = Assert.Single(diff.Lines, line => line.Kind == SectionDiffLineKind.Modified);

        Assert.Equal(6, modified.HeadLineNumber);
        Assert.Equal("before();", modified.HeadText);
        Assert.Equal(6, modified.WorkingLineNumber);
        Assert.Equal("after();", modified.WorkingText);
        Assert.Equal(1, diff.ModifiedLineCount);
        Assert.Equal(0, diff.AddedLineCount);
        Assert.Equal(0, diff.RemovedLineCount);
    }

    [Fact]
    public void Compare_AsymmetricHunkProducesModifiedAndAddedRows()
    {
        var head = Parse(Section("Change", "before();\nkeep();\n"));
        var working = Parse(Section("Change", "after();\nextra();\nkeep();\n"));
        var change = Assert.Single(new SectionComparer().Compare(head, working));

        var diff = new SectionDiffer().Compare(change, head, working);

        Assert.Equal(1, diff.ModifiedLineCount);
        Assert.Equal(1, diff.AddedLineCount);
        Assert.Equal(0, diff.RemovedLineCount);
        var added = Assert.Single(diff.Lines, line => line.Kind == SectionDiffLineKind.Added);
        Assert.Null(added.HeadLineNumber);
        Assert.Equal(7, added.WorkingLineNumber);
        Assert.Equal("extra();", added.WorkingText);
    }

    [Fact]
    public void Compare_AddedSectionHasOnlyWorkingTreeRows()
    {
        var working = Parse(Section("Added", "added();\n"));
        var change = Assert.Single(new SectionComparer().Compare(null, working));

        var diff = new SectionDiffer().Compare(change, null, working);

        Assert.Equal(6, diff.AddedLineCount);
        Assert.All(diff.Lines, line =>
        {
            Assert.Equal(SectionDiffLineKind.Added, line.Kind);
            Assert.Null(line.HeadLineNumber);
            Assert.NotNull(line.WorkingLineNumber);
        });
    }

    [Fact]
    public void Compare_RemovedSectionHasOnlyHeadRows()
    {
        var head = Parse(Section("Keep", "keep();\n") + Section("Removed", "removed();"));
        var working = Parse(Section("Keep", "keep();\n"));
        var change = Assert.Single(
            new SectionComparer().Compare(head, working),
            item => item.Kind == SectionChangeKind.Removed);

        var diff = new SectionDiffer().Compare(change, head, working);

        Assert.Equal(6, diff.RemovedLineCount);
        Assert.Equal(7, diff.Lines[0].HeadLineNumber);
        Assert.Equal("removed();", diff.Lines[^1].HeadText);
        Assert.All(diff.Lines, line => Assert.Null(line.WorkingLineNumber));
    }

    [Fact]
    public void Compare_HeadingDescriptionChangeIsIncludedInFullRangeDiff()
    {
        var head = Parse(DescribedSection("RIGHT CLICK", "old description", "run();\n"));
        var working = Parse(DescribedSection("RIGHT CLICK", "new description", "run();\n"));
        var change = Assert.Single(new SectionComparer().Compare(head, working));

        var diff = new SectionDiffer().Compare(change, head, working);
        var modified = Assert.Single(diff.Lines, line => line.Kind == SectionDiffLineKind.Modified);

        Assert.Equal(" * old description", modified.HeadText);
        Assert.Equal(" * new description", modified.WorkingText);
    }

    [Fact]
    public void SampleUserscript_AddedTransformLineProducesOneAlignedDiffWithoutChangingFixture()
    {
        var samplePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.js");
        var originalText = File.ReadAllText(samplePath);
        var parser = new SectionParser();
        var head = parser.Parse(samplePath, originalText);
        var transformIndex = head.Sections
            .Select((section, index) => (section, index))
            .Single(item => item.section.Name == "Transform")
            .index;
        var originalContent = head.Sections[transformIndex].ContentRange.GetText(head.Text);
        var working = new SourceDocumentEditor(parser).ReplaceSectionContent(
            head,
            transformIndex,
            "// ModuLens integration change\n" + originalContent);
        var change = Assert.Single(
            new SectionComparer().Compare(head, working),
            item => item.Identity.Name == "Transform");

        var diff = new SectionDiffer().Compare(change, head, working);

        Assert.Equal(21, working.Sections.Count);
        Assert.Equal(SectionChangeKind.Modified, change.Kind);
        Assert.Equal(1, diff.AddedLineCount);
        var added = Assert.Single(diff.Lines, line => line.Kind == SectionDiffLineKind.Added);
        Assert.Equal(1605, added.WorkingLineNumber);
        Assert.Equal("// ModuLens integration change", added.WorkingText);
        Assert.Equal(originalText, File.ReadAllText(samplePath));
    }

    private static ModuLens.Core.Documents.SourceDocument Parse(string source) =>
        new SectionParser().Parse("fixture.js", source);

    private static string Section(string name, string content) =>
        $"/**\n * ===\n * {name}\n * ===\n */\n{content}";

    private static string DescribedSection(string name, string description, string content) =>
        $"/**\n * ===\n * {name}\n * {description}\n * ===\n */\n{content}";
}
