using System.ComponentModel;
using System.Runtime.CompilerServices;
using ModuLens.Core.Documents;
using ModuLens.Core.Editing;
using ModuLens.Core.Parsing;

namespace ModuLens.App.ViewModels;

/// <summary>
/// Holds module selection and transient editing state independently of WPF controls.
/// </summary>
public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly SectionParser parser;
    private readonly SourceDocumentEditor editor;
    private SourceDocument? document;
    private IReadOnlyList<SourceSection> sections = Array.Empty<SourceSection>();
    private SourceSection? selectedSection;
    private string selectedSource = string.Empty;
    private string savedText = string.Empty;
    private string filePath = "No file selected";
    private string statusMessage = "Open a JavaScript file to explore its logical modules.";
    private bool hasUnsavedChanges;
    private bool isChangingSelection;

    /// <summary>Initializes the view model with the production section parser.</summary>
    public MainWindowViewModel()
        : this(new SectionParser())
    {
    }

    /// <summary>Initializes the view model with an explicit section parser.</summary>
    /// <param name="parser">The parser used to create the document snapshot.</param>
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

    /// <summary>Gets the detected source sections in source order.</summary>
    public IReadOnlyList<SourceSection> Sections
    {
        get => sections;
        private set => SetField(ref sections, value);
    }

    /// <summary>Gets or sets the section selected by the module explorer.</summary>
    public SourceSection? SelectedSection
    {
        get => selectedSection;
        set => SelectSection(value);
    }

    /// <summary>Gets or sets the editable text for the selected content range.</summary>
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
            }
        }
    }

    /// <summary>Gets whether a module is available for editing.</summary>
    public bool HasSelectedSection => SelectedSection is not null;

    /// <summary>Gets whether the in-memory source differs from the loaded file.</summary>
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

    /// <summary>Gets a concise line-range description for the selected section.</summary>
    public string SelectedRangeSummary
    {
        get
        {
            if (SelectedSection is null)
            {
                return "No module selected";
            }

            var content = SelectedSection.ContentRange;
            var contentText = content.IsEmpty
                ? $"empty content at line {content.StartLine}"
                : $"content {content.StartLine}–{content.EndLine}";

            return $"{contentText}  •  full {SelectedSection.FullRange.StartLine}–{SelectedSection.FullRange.EndLine}";
        }
    }

    /// <summary>Gets a user-facing load or selection status.</summary>
    public string StatusMessage
    {
        get => statusMessage;
        private set => SetField(ref statusMessage, value);
    }

    /// <summary>
    /// Replaces the current snapshot with a parsed source document.
    /// </summary>
    /// <param name="sourceFilePath">The path displayed to the user.</param>
    /// <param name="sourceText">The exact source text read by the UI adapter.</param>
    public void LoadDocument(string sourceFilePath, string sourceText)
    {
        var parsedDocument = parser.Parse(sourceFilePath, sourceText);

        isChangingSelection = true;
        try
        {
            document = parsedDocument;
            savedText = parsedDocument.Text;
            FilePath = parsedDocument.FilePath;
            Sections = parsedDocument.Sections;
            SetSelectionCore(Sections.FirstOrDefault());
        }
        finally
        {
            isChangingSelection = false;
        }

        RefreshDirtyState();
        StatusMessage = Sections.Count == 0
            ? "No supported section headings were found."
            : $"{Sections.Count} modules detected.";
    }

    /// <summary>
    /// Applies the active editor buffer to the complete document and returns the
    /// exact source text that should be passed to the file adapter.
    /// </summary>
    /// <returns>The complete updated source text.</returns>
    public string PrepareSave()
    {
        if (document is null)
        {
            throw new InvalidOperationException("No source document is loaded.");
        }

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
    }

    private void SelectSection(SourceSection? requestedSection)
    {
        if (isChangingSelection || ReferenceEquals(selectedSection, requestedSection))
        {
            return;
        }

        isChangingSelection = true;
        try
        {
            var requestedStartOffset = requestedSection?.FullRange.StartOffset;
            var previousContentEndOffset = selectedSection?.ContentRange.EndOffset;
            var editDelta = CommitSelectedEdit();

            SourceSection? mappedSection;
            if (requestedStartOffset is null || document is null)
            {
                mappedSection = null;
            }
            else
            {
                var mappedStartOffset = requestedStartOffset.Value;
                if (previousContentEndOffset is not null &&
                    mappedStartOffset >= previousContentEndOffset.Value)
                {
                    mappedStartOffset += editDelta;
                }

                mappedSection = document.Sections.FirstOrDefault(
                    section => section.FullRange.StartOffset == mappedStartOffset);
            }

            SetSelectionCore(mappedSection);
            StatusMessage = mappedSection is null && requestedSection is not null
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
        if (document is null || selectedSection is null)
        {
            return 0;
        }

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

        var reparsedSelection = document.Sections.FirstOrDefault(
            section => section.FullRange.StartOffset == selectedStartOffset);
        SetSelectionCore(reparsedSelection);
        RefreshDirtyState();
        return editDelta;
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

    private void SetSelectionCore(SourceSection? section)
    {
        selectedSection = section;
        selectedSource = document is null || section is null
            ? string.Empty
            : section.ContentRange.GetText(document.Text);

        OnPropertyChanged(nameof(SelectedSection));
        OnPropertyChanged(nameof(SelectedSource));
        OnPropertyChanged(nameof(SelectedRangeSummary));
        OnPropertyChanged(nameof(HasSelectedSection));
    }

    private void RefreshDirtyState()
    {
        var editorDiffers = document is not null && selectedSection is not null &&
            !string.Equals(
                selectedSource,
                selectedSection.ContentRange.GetText(document.Text),
                StringComparison.Ordinal);
        var documentDiffers = document is not null &&
            !string.Equals(document.Text, savedText, StringComparison.Ordinal);

        HasUnsavedChanges = editorDiffers || documentDiffers;
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
