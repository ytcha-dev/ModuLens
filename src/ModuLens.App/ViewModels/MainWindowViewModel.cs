using System.ComponentModel;
using System.Runtime.CompilerServices;
using ModuLens.Core.Documents;
using ModuLens.Core.Parsing;

namespace ModuLens.App.ViewModels;

/// <summary>
/// Holds the read-only module explorer state independently of WPF controls.
/// </summary>
public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly SectionParser parser;
    private SourceDocument? document;
    private IReadOnlyList<SourceSection> sections = Array.Empty<SourceSection>();
    private SourceSection? selectedSection;
    private string filePath = "No file selected";
    private string statusMessage = "Open a JavaScript file to explore its logical modules.";

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
        set
        {
            if (!SetField(ref selectedSection, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedSource));
            OnPropertyChanged(nameof(SelectedRangeSummary));
        }
    }

    /// <summary>Gets the exact raw text in the selected section's content range.</summary>
    public string SelectedSource =>
        document is null || SelectedSection is null
            ? string.Empty
            : SelectedSection.ContentRange.GetText(document.Text);

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

        document = parsedDocument;
        FilePath = parsedDocument.FilePath;
        Sections = parsedDocument.Sections;
        SelectedSection = Sections.FirstOrDefault();
        StatusMessage = Sections.Count == 0
            ? "No supported section headings were found."
            : $"{Sections.Count} modules detected.";

        OnPropertyChanged(nameof(SelectedSource));
        OnPropertyChanged(nameof(SelectedRangeSummary));
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
