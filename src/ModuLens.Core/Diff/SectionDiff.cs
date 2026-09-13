using System.Collections.ObjectModel;
using ModuLens.Core.Git;

namespace ModuLens.Core.Diff;

/// <summary>Classifies one aligned row in a module diff.</summary>
public enum SectionDiffLineKind
{
    /// <summary>The same line exists on both sides.</summary>
    Unchanged,

    /// <summary>A HEAD line is aligned with a different working-tree line.</summary>
    Modified,

    /// <summary>The line exists only in the working tree.</summary>
    Added,

    /// <summary>The line exists only in HEAD.</summary>
    Removed,
}

/// <summary>Represents one aligned side-by-side row.</summary>
public sealed record SectionDiffLine(
    int? HeadLineNumber,
    string? HeadText,
    int? WorkingLineNumber,
    string? WorkingText,
    SectionDiffLineKind Kind);

/// <summary>Contains the complete line diff for one logical section identity.</summary>
public sealed class SectionDiff
{
    /// <summary>Initializes an immutable section diff.</summary>
    public SectionDiff(
        SectionIdentity identity,
        SectionChangeKind changeKind,
        IEnumerable<SectionDiffLine> lines)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(lines);

        Identity = identity;
        ChangeKind = changeKind;
        Lines = new ReadOnlyCollection<SectionDiffLine>(lines.ToArray());
    }

    /// <summary>Gets the exact-name and occurrence identity being compared.</summary>
    public SectionIdentity Identity { get; }

    /// <summary>Gets the section-level comparison result.</summary>
    public SectionChangeKind ChangeKind { get; }

    /// <summary>Gets aligned rows in source order.</summary>
    public IReadOnlyList<SectionDiffLine> Lines { get; }

    /// <summary>Gets the number of aligned replacement rows.</summary>
    public int ModifiedLineCount => Lines.Count(line => line.Kind == SectionDiffLineKind.Modified);

    /// <summary>Gets the number of working-tree-only rows.</summary>
    public int AddedLineCount => Lines.Count(line => line.Kind == SectionDiffLineKind.Added);

    /// <summary>Gets the number of HEAD-only rows.</summary>
    public int RemovedLineCount => Lines.Count(line => line.Kind == SectionDiffLineKind.Removed);
}
