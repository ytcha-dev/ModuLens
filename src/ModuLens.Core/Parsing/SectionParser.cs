using ModuLens.Core.Documents;

namespace ModuLens.Core.Parsing;

/// <summary>
/// Detects the narrow structured JSDoc heading grammar defined by PRD 0.2.
/// </summary>
public sealed class SectionParser
{
    /// <summary>Parses source text without changing it or accessing the filesystem.</summary>
    /// <param name="filePath">The source path to retain as document metadata.</param>
    /// <param name="sourceText">The exact source text to parse.</param>
    /// <returns>A source document containing sections in source order.</returns>
    public SourceDocument Parse(string filePath, string sourceText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(sourceText);

        var lines = ReadLines(sourceText);
        var headings = DetectHeadings(lines);
        var sections = BuildSections(sourceText, lines, headings);

        return new SourceDocument(filePath, sourceText, lines.Count, sections);
    }

    private static IReadOnlyList<Heading> DetectHeadings(IReadOnlyList<SourceLine> lines)
    {
        var headings = new List<Heading>();

        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            if (!IsOpeningDelimiter(lines[lineIndex].Text))
            {
                continue;
            }

            var closingLineIndex = FindClosingDelimiter(lines, lineIndex + 1);
            if (closingLineIndex < 0)
            {
                continue;
            }

            if (TryGetHeadingName(lines, lineIndex, closingLineIndex, out var name))
            {
                headings.Add(new Heading(name, lineIndex, closingLineIndex));
            }

            lineIndex = closingLineIndex;
        }

        return headings;
    }

    private static int FindClosingDelimiter(IReadOnlyList<SourceLine> lines, int startIndex)
    {
        for (var lineIndex = startIndex; lineIndex < lines.Count; lineIndex++)
        {
            var trimmed = lines[lineIndex].Text.Trim();

            if (trimmed == "*/")
            {
                return lineIndex;
            }

            if (trimmed == "/**")
            {
                return -1;
            }
        }

        return -1;
    }

    private static bool TryGetHeadingName(
        IReadOnlyList<SourceLine> lines,
        int openingLineIndex,
        int closingLineIndex,
        out string name)
    {
        name = string.Empty;
        var content = new List<string>();

        for (var lineIndex = openingLineIndex + 1; lineIndex < closingLineIndex; lineIndex++)
        {
            var trimmed = lines[lineIndex].Text.Trim();
            if (trimmed.Length == 0)
            {
                content.Add(string.Empty);
                continue;
            }

            if (!trimmed.StartsWith('*'))
            {
                return false;
            }

            content.Add(trimmed[1..].Trim());
        }

        var nonEmptyContent = content.Where(value => value.Length > 0).ToArray();
        if (nonEmptyContent.Length < 3 ||
            !IsSeparator(nonEmptyContent[0]) ||
            IsSeparator(nonEmptyContent[1]) ||
            !IsSeparator(nonEmptyContent[^1]))
        {
            return false;
        }

        name = nonEmptyContent[1];
        return true;
    }

    private static IReadOnlyList<SourceSection> BuildSections(
        string sourceText,
        IReadOnlyList<SourceLine> lines,
        IReadOnlyList<Heading> headings)
    {
        var sections = new List<SourceSection>(headings.Count);

        for (var headingIndex = 0; headingIndex < headings.Count; headingIndex++)
        {
            var heading = headings[headingIndex];
            var openingLine = lines[heading.OpeningLineIndex];
            var closingLine = lines[heading.ClosingLineIndex];
            var nextHeadingStartLine = headingIndex + 1 < headings.Count
                ? lines[headings[headingIndex + 1].OpeningLineIndex]
                : null;

            var fullStartOffset = openingLine.StartOffset;
            var fullEndOffset = nextHeadingStartLine?.StartOffset ?? sourceText.Length;
            var fullEndLine = nextHeadingStartLine?.Number - 1 ?? lines.Count;

            var headerRange = new SourceRange(
                fullStartOffset,
                closingLine.EndOffset,
                openingLine.Number,
                closingLine.Number);

            var contentStartLine = closingLine.Number + 1;
            var contentIsEmpty = closingLine.EndOffset == fullEndOffset;
            var contentRange = new SourceRange(
                closingLine.EndOffset,
                fullEndOffset,
                contentStartLine,
                contentIsEmpty ? contentStartLine - 1 : fullEndLine);

            var fullRange = new SourceRange(
                fullStartOffset,
                fullEndOffset,
                openingLine.Number,
                fullEndLine);

            sections.Add(new SourceSection(
                heading.Name,
                fullRange,
                headerRange,
                contentRange));
        }

        return sections;
    }

    private static IReadOnlyList<SourceLine> ReadLines(string sourceText)
    {
        var lines = new List<SourceLine>();
        var position = 0;
        var lineNumber = 1;

        while (position < sourceText.Length)
        {
            var startOffset = position;

            while (position < sourceText.Length && sourceText[position] is not ('\r' or '\n'))
            {
                position++;
            }

            var contentEndOffset = position;

            if (position < sourceText.Length && sourceText[position] == '\r')
            {
                position++;
                if (position < sourceText.Length && sourceText[position] == '\n')
                {
                    position++;
                }
            }
            else if (position < sourceText.Length && sourceText[position] == '\n')
            {
                position++;
            }

            lines.Add(new SourceLine(
                lineNumber,
                startOffset,
                position,
                sourceText[startOffset..contentEndOffset]));
            lineNumber++;
        }

        return lines;
    }

    private static bool IsOpeningDelimiter(string text) => text.Trim() == "/**";

    private static bool IsSeparator(string text) =>
        text.Length >= 3 && text.All(character => character == '=');

    private sealed record SourceLine(
        int Number,
        int StartOffset,
        int EndOffset,
        string Text);

    private sealed record Heading(
        string Name,
        int OpeningLineIndex,
        int ClosingLineIndex);
}
