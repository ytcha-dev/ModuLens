using ModuLens.Core.Documents;
using ModuLens.Core.Git;

namespace ModuLens.App.ViewModels;

/// <summary>Projects one compared section identity into the module explorer.</summary>
public sealed class ModuleListItemViewModel
{
    /// <summary>Initializes a module explorer item.</summary>
    public ModuleListItemViewModel(
        SectionIdentity identity,
        SourceSection? headSection,
        SourceSection? workingSection,
        SectionChangeKind? gitStatus)
    {
        ArgumentNullException.ThrowIfNull(identity);
        Identity = identity;
        HeadSection = headSection;
        WorkingSection = workingSection;
        GitStatus = gitStatus;
    }

    /// <summary>Gets the cross-version section identity.</summary>
    public SectionIdentity Identity { get; }

    /// <summary>Gets the module name shown in the explorer.</summary>
    public string Name => Identity.OccurrenceIndex == 1
        ? Identity.Name
        : $"{Identity.Name} ({Identity.OccurrenceIndex})";

    /// <summary>Gets the HEAD section, when one exists.</summary>
    public SourceSection? HeadSection { get; }

    /// <summary>Gets the current section, when one exists.</summary>
    public SourceSection? WorkingSection { get; }

    /// <summary>Gets the Git comparison state, or null when Git is unavailable.</summary>
    public SectionChangeKind? GitStatus { get; }

    /// <summary>Gets the lowercase status label displayed beside the module.</summary>
    public string GitStatusLabel => GitStatus?.ToString().ToLowerInvariant() ?? "not compared";
}
