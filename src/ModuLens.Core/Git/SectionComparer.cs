using ModuLens.Core.Documents;

namespace ModuLens.Core.Git;

/// <summary>Projects exact source differences onto logical section identities.</summary>
public sealed class SectionComparer
{
    /// <summary>
    /// Compares HEAD with current source using exact name and 1-based occurrence
    /// index as section identity.
    /// </summary>
    /// <param name="headDocument">
    /// The parsed HEAD version, or <see langword="null"/> when the file is absent
    /// from HEAD.
    /// </param>
    /// <param name="workingDocument">The parsed current source.</param>
    /// <returns>
    /// Current sections in source order followed by HEAD-only removed sections in
    /// their HEAD source order.
    /// </returns>
    public IReadOnlyList<SectionChange> Compare(
        SourceDocument? headDocument,
        SourceDocument workingDocument)
    {
        ArgumentNullException.ThrowIfNull(workingDocument);

        var headEntries = CreateEntries(headDocument?.Sections ?? []);
        var workingEntries = CreateEntries(workingDocument.Sections);
        var headByIdentity = headEntries.ToDictionary(entry => entry.Identity);
        var workingIdentities = workingEntries
            .Select(entry => entry.Identity)
            .ToHashSet();
        var changes = new List<SectionChange>(headEntries.Count + workingEntries.Count);

        foreach (var workingEntry in workingEntries)
        {
            if (!headByIdentity.TryGetValue(workingEntry.Identity, out var headEntry))
            {
                changes.Add(new SectionChange(
                    workingEntry.Identity,
                    SectionChangeKind.Added,
                    null,
                    workingEntry.Section));
                continue;
            }

            var headText = headEntry.Section.FullRange.GetText(headDocument!.Text);
            var workingText = workingEntry.Section.FullRange.GetText(workingDocument.Text);
            var kind = string.Equals(headText, workingText, StringComparison.Ordinal)
                ? SectionChangeKind.Unchanged
                : SectionChangeKind.Modified;
            changes.Add(new SectionChange(
                workingEntry.Identity,
                kind,
                headEntry.Section,
                workingEntry.Section));
        }

        foreach (var headEntry in headEntries)
        {
            if (!workingIdentities.Contains(headEntry.Identity))
            {
                changes.Add(new SectionChange(
                    headEntry.Identity,
                    SectionChangeKind.Removed,
                    headEntry.Section,
                    null));
            }
        }

        return changes;
    }

    private static IReadOnlyList<SectionEntry> CreateEntries(
        IReadOnlyList<SourceSection> sections)
    {
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        var entries = new List<SectionEntry>(sections.Count);

        foreach (var section in sections)
        {
            occurrences.TryGetValue(section.Name, out var previousCount);
            var occurrenceIndex = previousCount + 1;
            occurrences[section.Name] = occurrenceIndex;
            entries.Add(new SectionEntry(
                new SectionIdentity(section.Name, occurrenceIndex),
                section));
        }

        return entries;
    }

    private sealed record SectionEntry(
        SectionIdentity Identity,
        SourceSection Section);
}
