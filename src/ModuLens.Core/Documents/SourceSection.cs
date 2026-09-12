namespace ModuLens.Core.Documents;

/// <summary>
/// Represents one logical module inferred from a structured section heading.
/// </summary>
public sealed class SourceSection
{
    /// <summary>Initializes a source section and validates its range partition.</summary>
    /// <param name="name">The trimmed, non-empty section name.</param>
    /// <param name="fullRange">The complete header-and-content range.</param>
    /// <param name="headerRange">The complete heading JSDoc range.</param>
    /// <param name="contentRange">The raw content following the heading.</param>
    public SourceSection(
        string name,
        SourceRange fullRange,
        SourceRange headerRange,
        SourceRange contentRange)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(fullRange);
        ArgumentNullException.ThrowIfNull(headerRange);
        ArgumentNullException.ThrowIfNull(contentRange);

        if (fullRange.StartOffset != headerRange.StartOffset ||
            headerRange.EndOffset != contentRange.StartOffset ||
            contentRange.EndOffset != fullRange.EndOffset)
        {
            throw new ArgumentException(
                "HeaderRange and ContentRange must form an exact partition of FullRange.");
        }

        Name = name.Trim();
        FullRange = fullRange;
        HeaderRange = headerRange;
        ContentRange = contentRange;
    }

    /// <summary>Gets the normalized section name.</summary>
    public string Name { get; }

    /// <summary>Gets the complete section range, including header and content.</summary>
    public SourceRange FullRange { get; }

    /// <summary>Gets the range occupied by the complete heading JSDoc.</summary>
    public SourceRange HeaderRange { get; }

    /// <summary>Gets the raw content range following the heading.</summary>
    public SourceRange ContentRange { get; }
}
