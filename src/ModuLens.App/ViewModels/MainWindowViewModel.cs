using System.ComponentModel;
using System.Runtime.CompilerServices;
using ModuLens.Core.Diff;
using ModuLens.Core.Documents;
using ModuLens.Core.Editing;
using ModuLens.Core.Git;
using ModuLens.Core.Parsing;

namespace ModuLens.App.ViewModels;

/// <summary>
/// Holds module selection, editing, and Git comparison state independently of
/// WPF controls and process execution.
/// </summary>
public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly SectionParser parser;
    private readonly SourceDocumentEditor editor;
    private readonly SectionComparer sectionComparer = new();
    private readonly SectionDiffer sectionDiffer = new();
    private SourceDocument? document;
    private SourceDocument? headDocument;
    private GitFileBaselineKind? gitBaselineKind;
    private string? headRevision;
    private IReadOnlyList<SourceSection> sections = Array.Empty<SourceSection>();
    private IReadOnlyList<ModuleListItemViewModel> modules = Array.Empty<ModuleListItemViewModel>();
    private IReadOnlyList<ModuleDiffLineViewModel> selectedDiffLines = Array.Empty<ModuleDiffLineViewModel>();
    private ModuleListItemViewModel? selectedModule;
    private ModuleDiffLineViewModel? selectedDiffLine;
    private string selectedSource = string.Empty;
    private string savedText = string.Empty;
    private string filePath = "No file selected";
    private string statusMessage = "Open a JavaScript file to explore its logical modules.";
    private string gitStatusMessage = "Git status has not been checked.";
    private string diffSummary = "Git comparison is not available.";
    private string diffPositionSummary = "No changes";
    private string headDiffRangeSummary = "HEAD: unavailable";
    private string workingDiffRangeSummary = "Working Tree: unavailable";
    private bool hasUnsavedChanges;
    private bool hasUnmappedGitChanges;
    private bool canShowDiff;
    private bool isChangingSelection;
    private int selectedDetailTabIndex;

    /// <summary>Initializes the view model with production Core services.</summary>
    public MainWindowViewModel()
        : this(new SectionParser())
    {
    }

    /// <summary>Initializes the view model with an explicit section parser.</summary>
    /// <param name="parser">The parser used for current and HEAD snapshots.</param>
    public MainWindowViewModel(SectionParser parser)
    {
        ArgumentNullException.ThrowIfNull(parser);
        this.parser = parser;
        editor = new SourceDocumentEditor(parser);
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the loaded file path or the initial empty-state label.</summary>
    public string FilePath
    {
        get => filePath;
        private set => SetField(ref filePath, value);
    }

    /// <summary>Gets current source sections in source order.</summary>
    public IReadOnlyList<SourceSection> Sections
    {
        get => sections;
        private set => SetField(ref sections, value);
    }

    /// <summary>Gets module explorer items including HEAD-only removed modules.</summary>
    public IReadOnlyList<ModuleListItemViewModel> Modules
    {
        get => modules;
        private set => SetField(ref modules, value);
    }

    /// <summary>Gets or sets the module explorer selection.</summary>
    public ModuleListItemViewModel? SelectedModule
    {
        get => selectedModule;
        set => SelectModule(value);
    }

    /// <summary>
    /// Gets or sets the selected current section. This facade keeps source-level
    /// callers independent of the explorer projection.
    /// </summary>
    public SourceSection? SelectedSection
    {
        get => SelectedModule?.WorkingSection;
        set => SelectedModule = value is null
            ? null
            : Modules.FirstOrDefault(module => ReferenceEquals(module.WorkingSection, value));
    }

    /// <summary>Gets or sets the editable text for the selected current section.</summary>
    public string SelectedSource
    {
        get => selectedSource;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (!SetField(ref selectedSource, value))
            {
                return;
            }

            RefreshDirtyState();
            if (HasUnsavedChanges)
            {
                StatusMessage = "Unsaved changes.";
                GitStatusMessage = "Apply or save the editor buffer to refresh module status.";
            }
            else
            {
                StatusMessage = $"{Sections.Count} modules detected.";
                UpdateGitStatusMessage();
            }
        }
    }

    /// <summary>Gets whether a current module is available for editing.</summary>
    public bool HasSelectedSection => SelectedSection is not null;

    /// <summary>Gets aligned side-by-side rows for the selected module.</summary>
    public IReadOnlyList<ModuleDiffLineViewModel> SelectedDiffLines
    {
        get => selectedDiffLines;
        private set => SetField(ref selectedDiffLines, value);
    }

    /// <summary>Gets or sets the active row used for diff-hunk navigation.</summary>
    public ModuleDiffLineViewModel? SelectedDiffLine
    {
        get => selectedDiffLine;
        set
        {
            if (!SetField(ref selectedDiffLine, value))
            {
                return;
            }

            RefreshDiffNavigationState();
        }
    }

    /// <summary>Gets whether the selected module has an available Git baseline.</summary>
    public bool CanShowDiff
    {
        get => canShowDiff;
        private set => SetField(ref canShowDiff, value);
    }

    /// <summary>Gets the selected module's line-change counts and baseline.</summary>
    public string DiffSummary
    {
        get => diffSummary;
        private set => SetField(ref diffSummary, value);
    }

    /// <summary>Gets the active diff hunk position, such as Change 1 of 3.</summary>
    public string DiffPositionSummary
    {
        get => diffPositionSummary;
        private set => SetField(ref diffPositionSummary, value);
    }

    /// <summary>Gets the selected module's complete HEAD source range.</summary>
    public string HeadDiffRangeSummary
    {
        get => headDiffRangeSummary;
        private set => SetField(ref headDiffRangeSummary, value);
    }

    /// <summary>Gets the selected module's complete working-tree source range.</summary>
    public string WorkingDiffRangeSummary
    {
        get => workingDiffRangeSummary;
        private set => SetField(ref workingDiffRangeSummary, value);
    }

    /// <summary>
    /// Gets or sets the Current (0) or Diff (1) detail tab. Opening Diff first
    /// applies the active editor buffer to the in-memory document snapshot.
    /// </summary>
    public int SelectedDetailTabIndex
    {
        get => selectedDetailTabIndex;
        set
        {
            if (value == 1 && !isChangingSelection)
            {
                CommitPendingEditorSafely();
                RefreshSelectedDiff();
            }

            SetField(ref selectedDetailTabIndex, value);
        }
    }

    /// <summary>Gets whether an earlier changed module exists.</summary>
    public bool CanSelectPreviousChangedModule => FindAdjacentChangedModule(-1) is not null;

    /// <summary>Gets whether a later changed module exists.</summary>
    public bool CanSelectNextChangedModule => FindAdjacentChangedModule(1) is not null;

    /// <summary>Gets whether an earlier changed hunk exists in this module.</summary>
    public bool CanSelectPreviousChange => FindAdjacentChangeStart(-1) is not null;

    /// <summary>Gets whether a later changed hunk exists in this module.</summary>
    public bool CanSelectNextChange => FindAdjacentChangeStart(1) is not null;

    /// <summary>Selects the nearest earlier changed module.</summary>
    public void SelectPreviousChangedModule()
    {
        var module = FindAdjacentChangedModule(-1);
        if (module is not null)
        {
            SelectedModule = module;
        }
    }

    /// <summary>Selects the nearest later changed module.</summary>
    public void SelectNextChangedModule()
    {
        var module = FindAdjacentChangedModule(1);
        if (module is not null)
        {
            SelectedModule = module;
        }
    }

    /// <summary>Selects the previous changed hunk in the current module.</summary>
    public void SelectPreviousChange()
    {
        var line = FindAdjacentChangeStart(-1);
        if (line is not null)
        {
            SelectedDiffLine = line;
        }
    }

    /// <summary>Selects the next changed hunk in the current module.</summary>
    public void SelectNextChange()
    {
        var line = FindAdjacentChangeStart(1);
        if (line is not null)
        {
            SelectedDiffLine = line;
        }
    }

    /// <summary>Gets whether in-memory source differs from the loaded file.</summary>
    public bool HasUnsavedChanges
    {
        get => hasUnsavedChanges;
        private set
        {
            if (SetField(ref hasUnsavedChanges, value))
            {
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    /// <summary>Gets whether the current document has changes that can be saved.</summary>
    public bool CanSave => document is not null && HasUnsavedChanges;

    /// <summary>Gets a concise range description for the selected module.</summary>
    public string SelectedRangeSummary
    {
        get
        {
            if (SelectedModule is null)
            {
                return "No module selected";
            }

            if (SelectedSection is null)
            {
                return "Removed from working tree";
            }

            var content = SelectedSection.ContentRange;
            var contentText = content.IsEmpty
                ? $"empty content at line {content.StartLine}"
                : $"content {content.StartLine}–{content.EndLine}";

            return $"{contentText}  •  full {SelectedSection.FullRange.StartLine}–{SelectedSection.FullRange.EndLine}";
        }
    }

    /// <summary>Gets a user-facing load, edit, or save status.</summary>
    public string StatusMessage
    {
        get => statusMessage;
        private set => SetField(ref statusMessage, value);
    }

    /// <summary>Gets the current Git comparison summary.</summary>
    public string GitStatusMessage
    {
        get => gitStatusMessage;
        private set => SetField(ref gitStatusMessage, value);
    }

    /// <summary>Replaces the current state with a parsed source document.</summary>
    public void LoadDocument(string sourceFilePath, string sourceText)
    {
        var parsedDocument = parser.Parse(sourceFilePath, sourceText);

        isChangingSelection = true;
        try
        {
            document = parsedDocument;
            headDocument = null;
            headRevision = null;
            gitBaselineKind = null;
            savedText = parsedDocument.Text;
            FilePath = parsedDocument.FilePath;
            Sections = parsedDocument.Sections;
            RebuildModules();
            SetSelectionCore(Modules.FirstOrDefault(module => module.WorkingSection is not null));
        }
        finally
        {
            isChangingSelection = false;
        }

        RefreshDirtyState();
        StatusMessage = Sections.Count == 0
            ? "No supported section headings were found."
            : $"{Sections.Count} modules detected.";
        GitStatusMessage = "Git status has not been checked.";
    }

    /// <summary>Applies a Git baseline result to the current document.</summary>
    public void ApplyGitBaseline(GitFileBaseline baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);

        if (document is null)
        {
            throw new InvalidOperationException("No source document is loaded.");
        }

        CommitPendingEditorSafely();
        var selectedStartOffset = SelectedSection?.FullRange.StartOffset;
        var selectedIdentity = SelectedModule?.Identity;
        gitBaselineKind = baseline.Kind;
        headRevision = baseline.Revision;
        headDocument = baseline.Kind == GitFileBaselineKind.Available
            ? parser.Parse(document.FilePath, baseline.Text!)
            : null;

        isChangingSelection = true;
        try
        {
            RebuildModules();
            SetSelectionCore(FindMappedModule(selectedStartOffset, selectedIdentity));
        }
        finally
        {
            isChangingSelection = false;
        }

        UpdateGitStatusMessage();
    }

    /// <summary>Clears comparison badges after a Git adapter failure.</summary>
    public void SetGitUnavailable(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        CommitPendingEditorSafely();
        var selectedStartOffset = SelectedSection?.FullRange.StartOffset;
        gitBaselineKind = null;
        headRevision = null;
        headDocument = null;
        RebuildModules();
        SetSelectionCore(FindMappedModule(selectedStartOffset, null));
        GitStatusMessage = message;
    }

    /// <summary>Applies the active editor buffer and returns complete source text.</summary>
    public string PrepareSave()
    {
        if (document is null)
        {
            throw new InvalidOperationException("No source document is loaded.");
        }

        CommitPendingEditorSafely();

        return document.Text;
    }

    /// <summary>Marks the current in-memory source as successfully written.</summary>
    public void MarkSaved()
    {
        if (document is null)
        {
            throw new InvalidOperationException("No source document is loaded.");
        }

        savedText = document.Text;
        RefreshDirtyState();
        StatusMessage = $"Saved. {Sections.Count} modules detected.";
        UpdateGitStatusMessage();
    }

    private void SelectModule(ModuleListItemViewModel? requestedModule)
    {
        if (isChangingSelection || ReferenceEquals(selectedModule, requestedModule))
        {
            return;
        }

        isChangingSelection = true;
        try
        {
            var requestedStartOffset = requestedModule?.WorkingSection?.FullRange.StartOffset;
            var requestedIdentity = requestedModule?.Identity;
            var previousContentEndOffset = SelectedSection?.ContentRange.EndOffset;
            var editDelta = CommitSelectedEdit();

            if (requestedStartOffset is not null &&
                previousContentEndOffset is not null &&
                requestedStartOffset.Value >= previousContentEndOffset.Value)
            {
                requestedStartOffset += editDelta;
            }

            var mappedModule = FindMappedModule(requestedStartOffset, requestedIdentity);
            SetSelectionCore(mappedModule);
            StatusMessage = mappedModule is null && requestedModule is not null
                ? "The requested module no longer exists after reparsing."
                : HasUnsavedChanges
                    ? "Unsaved changes."
                    : $"{Sections.Count} modules detected.";
        }
        finally
        {
            isChangingSelection = false;
        }
    }

    private int CommitSelectedEdit()
    {
        if (document is null || SelectedSection is null)
        {
            return 0;
        }

        var selectedSection = SelectedSection;
        var currentContent = selectedSection.ContentRange.GetText(document.Text);
        if (string.Equals(currentContent, selectedSource, StringComparison.Ordinal))
        {
            return 0;
        }

        var sectionIndex = IndexOfSection(selectedSection);
        if (sectionIndex < 0)
        {
            throw new InvalidOperationException("The selected module does not belong to the current document.");
        }

        var selectedStartOffset = selectedSection.FullRange.StartOffset;
        var editDelta = selectedSource.Length - selectedSection.ContentRange.Length;
        document = editor.ReplaceSectionContent(document, sectionIndex, selectedSource);
        Sections = document.Sections;
        RebuildModules();
        SetSelectionCore(FindMappedModule(selectedStartOffset, null));
        RefreshDirtyState();
        UpdateGitStatusMessage();
        return editDelta;
    }

    private void CommitPendingEditorSafely()
    {
        var wasChangingSelection = isChangingSelection;
        isChangingSelection = true;
        try
        {
            CommitSelectedEdit();
        }
        finally
        {
            isChangingSelection = wasChangingSelection;
        }
    }

    private int IndexOfSection(SourceSection section)
    {
        for (var index = 0; index < Sections.Count; index++)
        {
            if (ReferenceEquals(Sections[index], section))
            {
                return index;
            }
        }

        return -1;
    }

    private void RebuildModules()
    {
        if (document is null)
        {
            Modules = [];
            hasUnmappedGitChanges = false;
            return;
        }

        if (gitBaselineKind is GitFileBaselineKind.Available or GitFileBaselineKind.NotPresentAtHead)
        {
            Modules = sectionComparer.Compare(headDocument, document)
                .Select(change => new ModuleListItemViewModel(
                    change.Identity,
                    change.HeadSection,
                    change.WorkingSection,
                    change.Kind))
                .ToArray();
            hasUnmappedGitChanges = gitBaselineKind == GitFileBaselineKind.Available &&
                !string.Equals(
                    GetUnsectionedPrefix(headDocument!),
                    GetUnsectionedPrefix(document),
                    StringComparison.Ordinal);
            return;
        }

        hasUnmappedGitChanges = false;
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        Modules = document.Sections.Select(section =>
        {
            occurrences.TryGetValue(section.Name, out var previousCount);
            var occurrenceIndex = previousCount + 1;
            occurrences[section.Name] = occurrenceIndex;
            return new ModuleListItemViewModel(
                new SectionIdentity(section.Name, occurrenceIndex),
                null,
                section,
                null);
        }).ToArray();
    }

    private ModuleListItemViewModel? FindMappedModule(
        int? workingStartOffset,
        SectionIdentity? identity)
    {
        if (workingStartOffset is not null)
        {
            var current = Modules.FirstOrDefault(module =>
                module.WorkingSection?.FullRange.StartOffset == workingStartOffset.Value);
            if (current is not null)
            {
                return current;
            }
        }

        return identity is null
            ? Modules.FirstOrDefault(module => module.WorkingSection is not null)
            : Modules.FirstOrDefault(module => module.Identity == identity);
    }

    private void SetSelectionCore(ModuleListItemViewModel? module)
    {
        selectedModule = module;
        selectedSource = document is null || module?.WorkingSection is null
            ? string.Empty
            : module.WorkingSection.ContentRange.GetText(document.Text);

        OnPropertyChanged(nameof(SelectedModule));
        OnPropertyChanged(nameof(SelectedSection));
        OnPropertyChanged(nameof(SelectedSource));
        OnPropertyChanged(nameof(SelectedRangeSummary));
        OnPropertyChanged(nameof(HasSelectedSection));
        OnPropertyChanged(nameof(CanSelectPreviousChangedModule));
        OnPropertyChanged(nameof(CanSelectNextChangedModule));
        RefreshSelectedDiff();
        SetSelectedDetailTabIndexCore(module?.GitStatus is
            SectionChangeKind.Modified or
            SectionChangeKind.Added or
            SectionChangeKind.Removed
                ? 1
                : 0);
    }

    private void RefreshSelectedDiff()
    {
        if (document is null || SelectedModule?.GitStatus is null)
        {
            SelectedDiffLines = [];
            SelectedDiffLine = null;
            RefreshDiffNavigationState();
            CanShowDiff = false;
            DiffSummary = "Git comparison is not available.";
            HeadDiffRangeSummary = "HEAD: unavailable";
            WorkingDiffRangeSummary = "Working Tree: unavailable";
            return;
        }

        var module = SelectedModule;
        var change = new SectionChange(
            module.Identity,
            module.GitStatus.Value,
            module.HeadSection,
            module.WorkingSection);
        var diff = sectionDiffer.Compare(change, headDocument, document);
        SelectedDiffLines = diff.Lines
            .Select(line => new ModuleDiffLineViewModel(line))
            .ToArray();
        var changeStarts = GetChangeStarts();
        SelectedDiffLine = changeStarts.Count == 0
            ? null
            : SelectedDiffLines[changeStarts[0]];
        RefreshDiffNavigationState();
        CanShowDiff = true;
        DiffSummary = BuildDiffSummary(diff);
        HeadDiffRangeSummary = BuildDiffRangeSummary("HEAD", module.HeadSection);
        WorkingDiffRangeSummary = BuildDiffRangeSummary("Working Tree", module.WorkingSection);
    }

    private string BuildDiffSummary(SectionDiff diff)
    {
        var revision = headRevision is null
            ? "HEAD"
            : headRevision[..Math.Min(7, headRevision.Length)];
        var counts = new List<string>();

        if (diff.ModifiedLineCount > 0)
        {
            counts.Add($"{diff.ModifiedLineCount} modified");
        }

        if (diff.AddedLineCount > 0)
        {
            counts.Add($"{diff.AddedLineCount} added");
        }

        if (diff.RemovedLineCount > 0)
        {
            counts.Add($"{diff.RemovedLineCount} removed");
        }

        return counts.Count == 0
            ? $"{revision} comparison • no line changes"
            : $"{revision} comparison • {string.Join(" • ", counts)}";
    }

    private static string BuildDiffRangeSummary(string label, SourceSection? section) =>
        section is null
            ? $"{label}: absent"
            : $"{label}: full lines {section.FullRange.StartLine}–{section.FullRange.EndLine}";

    private void SetSelectedDetailTabIndexCore(int value)
    {
        if (selectedDetailTabIndex == value)
        {
            return;
        }

        selectedDetailTabIndex = value;
        OnPropertyChanged(nameof(SelectedDetailTabIndex));
    }

    private ModuleListItemViewModel? FindAdjacentChangedModule(int direction)
    {
        if (direction is not (-1 or 1) || SelectedModule is null)
        {
            return null;
        }

        var selectedIndex = -1;
        for (var index = 0; index < Modules.Count; index++)
        {
            if (ReferenceEquals(Modules[index], SelectedModule))
            {
                selectedIndex = index;
                break;
            }
        }

        for (var index = selectedIndex + direction;
             index >= 0 && index < Modules.Count;
             index += direction)
        {
            if (Modules[index].GitStatus is
                SectionChangeKind.Modified or
                SectionChangeKind.Added or
                SectionChangeKind.Removed)
            {
                return Modules[index];
            }
        }

        return null;
    }

    private ModuleDiffLineViewModel? FindAdjacentChangeStart(int direction)
    {
        if (direction is not (-1 or 1))
        {
            return null;
        }

        var starts = GetChangeStarts();
        if (starts.Count == 0)
        {
            return null;
        }

        var selectedIndex = SelectedDiffLine is null
            ? -1
            : IndexOfDiffLine(SelectedDiffLine);
        var selectedHunkIndex = GetSelectedHunkIndex(selectedIndex, starts);
        if (selectedHunkIndex >= 0)
        {
            var targetHunkIndex = selectedHunkIndex + direction;
            return targetHunkIndex >= 0 && targetHunkIndex < starts.Count
                ? SelectedDiffLines[starts[targetHunkIndex]]
                : null;
        }

        var candidateIndexes = direction > 0
            ? starts.Where(index => index > selectedIndex)
            : starts.Where(index => index < selectedIndex).Reverse();
        var candidate = candidateIndexes.FirstOrDefault(-1);
        return candidate >= 0 ? SelectedDiffLines[candidate] : null;
    }

    private IReadOnlyList<int> GetChangeStarts()
    {
        var starts = new List<int>();

        for (var index = 0; index < SelectedDiffLines.Count; index++)
        {
            if (SelectedDiffLines[index].KindLabel != "unchanged" &&
                (index == 0 || SelectedDiffLines[index - 1].KindLabel == "unchanged"))
            {
                starts.Add(index);
            }
        }

        return starts;
    }

    private int IndexOfDiffLine(ModuleDiffLineViewModel line)
    {
        for (var index = 0; index < SelectedDiffLines.Count; index++)
        {
            if (ReferenceEquals(SelectedDiffLines[index], line))
            {
                return index;
            }
        }

        return -1;
    }

    private int GetSelectedHunkIndex(int selectedIndex, IReadOnlyList<int> starts)
    {
        if (selectedIndex < 0 ||
            SelectedDiffLines[selectedIndex].KindLabel == "unchanged")
        {
            return -1;
        }

        for (var hunkIndex = 0; hunkIndex < starts.Count; hunkIndex++)
        {
            var end = hunkIndex + 1 < starts.Count
                ? starts[hunkIndex + 1]
                : SelectedDiffLines.Count;
            if (selectedIndex >= starts[hunkIndex] && selectedIndex < end)
            {
                return hunkIndex;
            }
        }

        return -1;
    }

    private void RefreshDiffNavigationState()
    {
        var starts = GetChangeStarts();
        var selectedIndex = SelectedDiffLine is null
            ? -1
            : IndexOfDiffLine(SelectedDiffLine);
        var selectedHunkIndex = GetSelectedHunkIndex(selectedIndex, starts);
        DiffPositionSummary = starts.Count == 0
            ? "No changes"
            : selectedHunkIndex >= 0
                ? $"Change {selectedHunkIndex + 1} of {starts.Count}"
                : $"{starts.Count} changes";
        OnPropertyChanged(nameof(CanSelectPreviousChange));
        OnPropertyChanged(nameof(CanSelectNextChange));
    }

    private void RefreshDirtyState()
    {
        var editorDiffers = document is not null && SelectedSection is not null &&
            !string.Equals(
                selectedSource,
                SelectedSection.ContentRange.GetText(document.Text),
                StringComparison.Ordinal);
        var documentDiffers = document is not null &&
            !string.Equals(document.Text, savedText, StringComparison.Ordinal);

        HasUnsavedChanges = editorDiffers || documentDiffers;
    }

    private void UpdateGitStatusMessage()
    {
        if (gitBaselineKind is null)
        {
            return;
        }

        if (gitBaselineKind == GitFileBaselineKind.NotInRepository)
        {
            GitStatusMessage = "Not in a Git repository.";
            return;
        }

        var changedCount = Modules.Count(module =>
            module.GitStatus is not SectionChangeKind.Unchanged);
        var moduleWord = changedCount == 1 ? "module" : "modules";
        var totalModuleWord = Sections.Count == 1 ? "module" : "modules";
        var scope = HasUnsavedChanges ? "Current source" : "Working tree";
        if (gitBaselineKind == GitFileBaselineKind.NotPresentAtHead)
        {
            GitStatusMessage = $"{scope}: not present at HEAD; {changedCount} added {moduleWord}.";
            return;
        }

        var revision = headRevision is null
            ? "HEAD"
            : headRevision[..Math.Min(7, headRevision.Length)];
        GitStatusMessage = changedCount == 0
            ? hasUnmappedGitChanges
                ? $"{scope} vs {revision}: modules unchanged; source outside detected modules changed."
                : $"{scope} vs {revision}: all {Sections.Count} {totalModuleWord} unchanged."
            : hasUnmappedGitChanges
                ? $"{scope} vs {revision}: {changedCount} changed {moduleWord}; source outside modules also changed."
                : $"{scope} vs {revision}: {changedCount} changed {moduleWord}.";
    }

    private static string GetUnsectionedPrefix(SourceDocument sourceDocument)
    {
        var firstSectionOffset = sourceDocument.Sections.FirstOrDefault()?.FullRange.StartOffset
            ?? sourceDocument.Text.Length;
        return sourceDocument.Text[..firstSectionOffset];
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
