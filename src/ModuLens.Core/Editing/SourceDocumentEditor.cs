using ModuLens.Core.Documents;
using ModuLens.Core.Parsing;

namespace ModuLens.Core.Editing;

/// <summary>
/// Replaces one section content range and reparses the complete source text.
/// </summary>
public sealed class SourceDocumentEditor
{
    private readonly SectionParser parser;

    /// <summary>Initializes an editor with the production section parser.</summary>
    public SourceDocumentEditor()
        : this(new SectionParser())
    {
    }

    /// <summary>Initializes an editor with an explicit parser.</summary>
    /// <param name="parser">The parser used to recalculate all source ranges.</param>
    public SourceDocumentEditor(SectionParser parser)
    {
        ArgumentNullException.ThrowIfNull(parser);
        this.parser = parser;
    }

    /// <summary>
    /// Replaces the exact content range at <paramref name="sectionIndex"/> and
    /// returns a newly parsed immutable document snapshot.
    /// </summary>
    /// <param name="document">The source snapshot to edit.</param>
    /// <param name="sectionIndex">The zero-based source-order section index.</param>
    /// <param name="replacementContent">The exact replacement text.</param>
    /// <returns>A new source document containing the complete updated text.</returns>
    public SourceDocument ReplaceSectionContent(
        SourceDocument document,
        int sectionIndex,
        string replacementContent)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(replacementContent);

        if ((uint)sectionIndex >= (uint)document.Sections.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sectionIndex),
                sectionIndex,
                "The section index must identify a section in the document snapshot.");
        }

        var contentRange = document.Sections[sectionIndex].ContentRange;
        var updatedText = string.Concat(
            document.Text.AsSpan(0, contentRange.StartOffset),
            replacementContent,
            document.Text.AsSpan(contentRange.EndOffset));

        return parser.Parse(document.FilePath, updatedText);
    }
}
