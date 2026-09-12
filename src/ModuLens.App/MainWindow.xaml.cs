using System.IO;
using System.Windows;
using Microsoft.Win32;
using ModuLens.App.ViewModels;

namespace ModuLens.App;

/// <summary>Hosts the read-only module explorer.</summary>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel = new();

    /// <summary>Initializes the main window.</summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void OpenFile_Click(object sender, RoutedEventArgs e)
    {
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
            var sourceText = await File.ReadAllTextAsync(dialog.FileName);
            viewModel.LoadDocument(dialog.FileName, sourceText);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Unable to open source file",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
