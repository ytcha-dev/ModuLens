using ModuLens.Core.Documents;

namespace ModuLens.Core.Git;

/// <summary>Identifies one same-named section occurrence across source versions.</summary>
public sealed record SectionIdentity
{
    /// <summary>Initializes and validates a section identity.</summary>
    /// <param name="name">The exact normalized section name.</param>
    /// <param name="occurrenceIndex">The 1-based same-name occurrence index.</param>
    public SectionIdentity(string name, int occurrenceIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(occurrenceIndex, 1);
        Name = name;
        OccurrenceIndex = occurrenceIndex;
    }

    /// <summary>Gets the exact normalized section name.</summary>
    public string Name { get; }

    /// <summary>Gets the 1-based same-name occurrence index.</summary>
    public int OccurrenceIndex { get; }
}

/// <summary>Describes how one logical section differs between HEAD and current source.</summary>
public sealed record SectionChange(
    SectionIdentity Identity,
    SectionChangeKind Kind,
    SourceSection? HeadSection,
    SourceSection? WorkingSection);

/// <summary>Classifies a section comparison against HEAD.</summary>
public enum SectionChangeKind
{
    /// <summary>The complete section text is identical in both versions.</summary>
    Unchanged,

    /// <summary>The identity exists in both versions but complete text differs.</summary>
    Modified,

    /// <summary>The identity exists only in current source.</summary>
    Added,

    /// <summary>The identity exists only in HEAD.</summary>
    Removed,
}
