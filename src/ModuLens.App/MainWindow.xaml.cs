using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using ModuLens.App.Storage;
using ModuLens.App.ViewModels;

namespace ModuLens.App;

/// <summary>Hosts the editable module explorer and filesystem adapter.</summary>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel = new();
    private readonly SourceFileStore sourceFileStore = new();
    private SourceFileSnapshot? sourceFileSnapshot;

    /// <summary>Initializes the main window.</summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardUnsavedChanges())
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Open JavaScript source",
            Filter = "JavaScript files (*.js)|*.js|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            sourceFileSnapshot = await sourceFileStore.LoadAsync(dialog.FileName);
            viewModel.LoadDocument(sourceFileSnapshot.FilePath, sourceFileSnapshot.Text);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Unable to open source file",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void SaveFile_Click(object sender, RoutedEventArgs e)
    {
        if (sourceFileSnapshot is null || !viewModel.CanSave)
        {
            return;
        }

        try
        {
            var updatedText = viewModel.PrepareSave();
            sourceFileSnapshot = await sourceFileStore.SaveAsync(sourceFileSnapshot, updatedText);
            viewModel.MarkSaved();
        }
        catch (SourceFileChangedException exception)
        {
            MessageBox.Show(
                this,
                $"{exception.Message}{Environment.NewLine}{Environment.NewLine}" +
                "Your edits remain in ModuLens. Copy them somewhere safe, then reopen and merge the external changes.",
                "Source file changed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or EncoderFallbackException)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Unable to save source file",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <inheritdoc />
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!ConfirmDiscardUnsavedChanges())
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }

    private bool ConfirmDiscardUnsavedChanges()
    {
        if (!viewModel.HasUnsavedChanges)
        {
            return true;
        }

        return MessageBox.Show(
            this,
            "Discard the unsaved module changes?",
            "Unsaved changes",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }
}
