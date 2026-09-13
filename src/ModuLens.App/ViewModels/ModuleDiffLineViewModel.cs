using ModuLens.Core.Diff;

namespace ModuLens.App.ViewModels;

/// <summary>Projects a Core diff row into display-friendly values.</summary>
public sealed class ModuleDiffLineViewModel
{
    /// <summary>Initializes a display row from a Core diff result.</summary>
    public ModuleDiffLineViewModel(SectionDiffLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        HeadLineNumber = line.HeadLineNumber?.ToString() ?? string.Empty;
        HeadText = line.HeadText ?? string.Empty;
        WorkingLineNumber = line.WorkingLineNumber?.ToString() ?? string.Empty;
        WorkingText = line.WorkingText ?? string.Empty;
        KindLabel = line.Kind.ToString().ToLowerInvariant();
    }

    /// <summary>Gets the HEAD line number, or an empty placeholder.</summary>
    public string HeadLineNumber { get; }

    /// <summary>Gets the HEAD line content, or an empty placeholder.</summary>
    public string HeadText { get; }

    /// <summary>Gets the working-tree line number, or an empty placeholder.</summary>
    public string WorkingLineNumber { get; }

    /// <summary>Gets the working-tree line content, or an empty placeholder.</summary>
    public string WorkingText { get; }

    /// <summary>Gets unchanged, modified, added, or removed for XAML styling.</summary>
    public string KindLabel { get; }
}
