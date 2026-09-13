using ModuLens.Core.Git;
using ModuLens.Core.Parsing;

namespace ModuLens.Core.Tests;

public sealed class SectionComparerTests
{
    [Fact]
    public void Compare_ClassifiesUnchangedModifiedAddedAndRemovedSections()
    {
        var head = Parse(
            Section("Keep", "keep();\n") +
            Section("Change", "before();\n") +
            Section("Remove", "remove();\n"));
        var working = Parse(
            Section("Keep", "keep();\n") +
            Section("Change", "after();\n") +
            Section("Add", "add();\n"));

        var changes = new SectionComparer().Compare(head, working);

        Assert.Collection(
            changes,
            change => AssertChange(change, "Keep", 1, SectionChangeKind.Unchanged),
            change => AssertChange(change, "Change", 1, SectionChangeKind.Modified),
            change => AssertChange(change, "Add", 1, SectionChangeKind.Added),
            change => AssertChange(change, "Remove", 1, SectionChangeKind.Removed));
        Assert.Null(changes[2].HeadSection);
        Assert.Null(changes[3].WorkingSection);
    }

    [Fact]
    public void Compare_UsesOneBasedOccurrenceIndexForDuplicateNames()
    {
        var head = Parse(
            Section("Duplicate", "first();\n") +
            Section("Duplicate", "second();\n"));
        var working = Parse(
            Section("Duplicate", "first();\n") +
            Section("Duplicate", "changed();\n"));

        var changes = new SectionComparer().Compare(head, working);

        Assert.Collection(
            changes,
            change => AssertChange(change, "Duplicate", 1, SectionChangeKind.Unchanged),
            change => AssertChange(change, "Duplicate", 2, SectionChangeKind.Modified));
    }

    [Fact]
    public void Compare_DoesNotUseLineRangesAsIdentity()
    {
        var head = Parse(Section("Stable", "stable();\n"));
        var working = Parse("// new preamble\n" + Section("Stable", "stable();\n"));

        var change = Assert.Single(new SectionComparer().Compare(head, working));

        Assert.Equal(SectionChangeKind.Unchanged, change.Kind);
        Assert.NotEqual(
            change.HeadSection?.FullRange.StartLine,
            change.WorkingSection?.FullRange.StartLine);
    }

    [Fact]
    public void Compare_WithoutHeadMarksEveryWorkingSectionAdded()
    {
        var working = Parse(Section("First", "first();\n") + Section("Second", "second();\n"));

        var changes = new SectionComparer().Compare(null, working);

        Assert.All(changes, change => Assert.Equal(SectionChangeKind.Added, change.Kind));
    }

    private static void AssertChange(
        SectionChange change,
        string name,
        int occurrenceIndex,
        SectionChangeKind kind)
    {
        Assert.Equal(new SectionIdentity(name, occurrenceIndex), change.Identity);
        Assert.Equal(kind, change.Kind);
    }

    private static ModuLens.Core.Documents.SourceDocument Parse(string source) =>
        new SectionParser().Parse("fixture.js", source);

    private static string Section(string name, string content) =>
        $"/**\n * ===\n * {name}\n * ===\n */\n{content}";
}
