using System.Collections.ObjectModel;

namespace ModuLens.Core.Documents;

/// <summary>Holds an immutable parsing snapshot of one source file.</summary>
public sealed class SourceDocument
{
    /// <summary>Initializes a parsed source document.</summary>
    /// <param name="filePath">The caller-supplied source file path.</param>
    /// <param name="text">The original, unmodified source text.</param>
    /// <param name="lineCount">The number of physical source lines.</param>
    /// <param name="sections">The detected sections in source order.</param>
    public SourceDocument(
        string filePath,
        string text,
        int lineCount,
        IEnumerable<SourceSection> sections)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(lineCount);
        ArgumentNullException.ThrowIfNull(sections);

        FilePath = filePath;
        Text = text;
        LineCount = lineCount;
        Sections = new ReadOnlyCollection<SourceSection>(sections.ToArray());
    }

    /// <summary>Gets the caller-supplied source file path.</summary>
    public string FilePath { get; }

    /// <summary>Gets the original source text without normalization.</summary>
    public string Text { get; }

    /// <summary>Gets the number of physical lines in <see cref="Text"/>.</summary>
    public int LineCount { get; }

    /// <summary>Gets the detected sections in source order.</summary>
    public IReadOnlyList<SourceSection> Sections { get; }
}
