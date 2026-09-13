using ModuLens.Core.Documents;
using ModuLens.Core.Git;

namespace ModuLens.Core.Diff;

/// <summary>Builds a deterministic, line-oriented side-by-side module diff.</summary>
public sealed class SectionDiffer
{
    /// <summary>
    /// Compares each section's complete <see cref="SourceSection.FullRange"/>,
    /// including its structured heading and raw content.
    /// </summary>
    /// <param name="change">The section identity and matched source ranges.</param>
    /// <param name="headDocument">The parsed HEAD source, when present.</param>
    /// <param name="workingDocument">The parsed current source.</param>
    public SectionDiff Compare(
        SectionChange change,
        SourceDocument? headDocument,
        SourceDocument workingDocument)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(workingDocument);

        if (change.HeadSection is not null && headDocument is null)
        {
            throw new ArgumentException(
                "A HEAD document is required when the change contains a HEAD section.",
                nameof(headDocument));
        }

        var headLines = change.HeadSection is null
            ? []
            : ReadLines(headDocument!, change.HeadSection);
        var workingLines = change.WorkingSection is null
            ? []
            : ReadLines(workingDocument, change.WorkingSection);

        return new SectionDiff(
            change.Identity,
            change.Kind,
            AlignLines(headLines, workingLines));
    }

    private static IReadOnlyList<SectionDiffLine> AlignLines(
        IReadOnlyList<SourceLine> headLines,
        IReadOnlyList<SourceLine> workingLines)
    {
        var longestCommonSubsequence = BuildLongestCommonSubsequenceTable(
            headLines,
            workingLines);
        var rows = new List<SectionDiffLine>(headLines.Count + workingLines.Count);
        var removedHunk = new List<SourceLine>();
        var addedHunk = new List<SourceLine>();
        var headIndex = 0;
        var workingIndex = 0;

        while (headIndex < headLines.Count || workingIndex < workingLines.Count)
        {
            if (headIndex < headLines.Count &&
                workingIndex < workingLines.Count &&
                string.Equals(
                    headLines[headIndex].Text,
                    workingLines[workingIndex].Text,
                    StringComparison.Ordinal))
            {
                FlushChangedHunk(rows, removedHunk, addedHunk);
                var headLine = headLines[headIndex++];
                var workingLine = workingLines[workingIndex++];
                rows.Add(new SectionDiffLine(
                    headLine.Number,
                    headLine.Text,
                    workingLine.Number,
                    workingLine.Text,
                    SectionDiffLineKind.Unchanged));
                continue;
            }

            if (workingIndex >= workingLines.Count ||
                (headIndex < headLines.Count &&
                 longestCommonSubsequence[headIndex + 1, workingIndex] >=
                 longestCommonSubsequence[headIndex, workingIndex + 1]))
            {
                removedHunk.Add(headLines[headIndex++]);
            }
            else
            {
                addedHunk.Add(workingLines[workingIndex++]);
            }
        }

        FlushChangedHunk(rows, removedHunk, addedHunk);
        return rows;
    }

    private static int[,] BuildLongestCommonSubsequenceTable(
        IReadOnlyList<SourceLine> headLines,
        IReadOnlyList<SourceLine> workingLines)
    {
        var lengths = new int[headLines.Count + 1, workingLines.Count + 1];

        for (var headIndex = headLines.Count - 1; headIndex >= 0; headIndex--)
        {
            for (var workingIndex = workingLines.Count - 1; workingIndex >= 0; workingIndex--)
            {
                lengths[headIndex, workingIndex] = string.Equals(
                    headLines[headIndex].Text,
                    workingLines[workingIndex].Text,
                    StringComparison.Ordinal)
                    ? lengths[headIndex + 1, workingIndex + 1] + 1
                    : Math.Max(
                        lengths[headIndex + 1, workingIndex],
                        lengths[headIndex, workingIndex + 1]);
            }
        }

        return lengths;
    }

    private static void FlushChangedHunk(
        ICollection<SectionDiffLine> rows,
        IList<SourceLine> removedLines,
        IList<SourceLine> addedLines)
    {
        var rowCount = Math.Max(removedLines.Count, addedLines.Count);

        for (var index = 0; index < rowCount; index++)
        {
            var headLine = index < removedLines.Count ? removedLines[index] : null;
            var workingLine = index < addedLines.Count ? addedLines[index] : null;
            var kind = headLine is not null && workingLine is not null
                ? SectionDiffLineKind.Modified
                : headLine is not null
                    ? SectionDiffLineKind.Removed
                    : SectionDiffLineKind.Added;

            rows.Add(new SectionDiffLine(
                headLine?.Number,
                headLine?.Text,
                workingLine?.Number,
                workingLine?.Text,
                kind));
        }

        removedLines.Clear();
        addedLines.Clear();
    }

    private static IReadOnlyList<SourceLine> ReadLines(
        SourceDocument document,
        SourceSection section)
    {
        var text = section.FullRange.GetText(document.Text);
        var lines = new List<SourceLine>();
        var position = 0;
        var lineNumber = section.FullRange.StartLine;

        while (position < text.Length)
        {
            var start = position;

            while (position < text.Length && text[position] is not ('\r' or '\n'))
            {
                position++;
            }

            var contentEnd = position;
            if (position < text.Length && text[position] == '\r')
            {
                position++;
                if (position < text.Length && text[position] == '\n')
                {
                    position++;
                }
            }
            else if (position < text.Length)
            {
                position++;
            }

            lines.Add(new SourceLine(lineNumber++, text[start..contentEnd]));
        }

        return lines;
    }

    private sealed record SourceLine(int Number, string Text);
}
