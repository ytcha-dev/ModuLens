namespace ModuLens.Core.Documents;

/// <summary>
/// Identifies an exact span in a <see cref="SourceDocument"/>.
/// </summary>
/// <remarks>
/// Character offsets use the half-open interval <c>[StartOffset, EndOffset)</c>.
/// For a non-empty range, <see cref="StartLine"/> and <see cref="EndLine"/> are
/// 1-based and inclusive. An empty range uses equal offsets and represents its
/// insertion point with <c>EndLine == StartLine - 1</c>.
/// </remarks>
public sealed record SourceRange
{
    /// <summary>Initializes a source range.</summary>
    /// <param name="startOffset">The zero-based inclusive character offset.</param>
    /// <param name="endOffset">The zero-based exclusive character offset.</param>
    /// <param name="startLine">The 1-based inclusive start line.</param>
    /// <param name="endLine">
    /// The 1-based inclusive end line, or <paramref name="startLine"/> minus one
    /// when the range is empty.
    /// </param>
    public SourceRange(int startOffset, int endOffset, int startLine, int endLine)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startOffset);

        if (endOffset < startOffset)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endOffset),
                endOffset,
                "The end offset must be greater than or equal to the start offset.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(startLine, 1);

        var isEmpty = startOffset == endOffset;
        var expectedMinimumEndLine = isEmpty ? startLine - 1 : startLine;

        if (endLine < expectedMinimumEndLine || (isEmpty && endLine != expectedMinimumEndLine))
        {
            throw new ArgumentException(
                isEmpty
                    ? "An empty range must use EndLine equal to StartLine minus one."
                    : "A non-empty range must end on or after its start line.",
                nameof(endLine));
        }

        StartOffset = startOffset;
        EndOffset = endOffset;
        StartLine = startLine;
        EndLine = endLine;
    }

    /// <summary>Gets the zero-based inclusive start offset.</summary>
    public int StartOffset { get; }

    /// <summary>Gets the zero-based exclusive end offset.</summary>
    public int EndOffset { get; }

    /// <summary>Gets the 1-based inclusive start line.</summary>
    public int StartLine { get; }

    /// <summary>
    /// Gets the 1-based inclusive end line, or <see cref="StartLine"/> minus one
    /// for an empty range.
    /// </summary>
    public int EndLine { get; }

    /// <summary>Gets the number of UTF-16 characters in the range.</summary>
    public int Length => EndOffset - StartOffset;

    /// <summary>Gets whether the range contains no characters.</summary>
    public bool IsEmpty => Length == 0;

    /// <summary>Returns the exact source text covered by this range.</summary>
    /// <param name="sourceText">The source text that owns the range.</param>
    /// <returns>The covered text, without normalization.</returns>
    public string GetText(string sourceText)
    {
        ArgumentNullException.ThrowIfNull(sourceText);

        if (EndOffset > sourceText.Length)
        {
            throw new ArgumentException(
                "The source text is shorter than the range end offset.",
                nameof(sourceText));
        }

        return sourceText[StartOffset..EndOffset];
    }
}
