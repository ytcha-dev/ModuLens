using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ModuLens.App.ViewModels;

namespace ModuLens.App.Tests;

public sealed class MainWindowSmokeTests
{
    [Fact]
    public void MainWindow_CanInitializeItsXamlOnAnStaThread()
    {
        Exception? capturedException = null;

        var thread = new Thread(() =>
        {
            try
            {
                var window = new MainWindow();
                Assert.IsType<MainWindowViewModel>(window.DataContext);
                var editor = Assert.IsType<TextBox>(window.FindName("ModuleEditor"));
                var saveButton = Assert.IsType<Button>(window.FindName("SaveButton"));
                var detailTabs = Assert.IsType<TabControl>(window.FindName("ModuleDetailTabs"));
                var diffTab = Assert.IsType<TabItem>(window.FindName("DiffTab"));
                var diffList = Assert.IsType<ListBox>(window.FindName("ModuleDiffList"));
                var previousChangeButton = Assert.IsType<Button>(
                    window.FindName("PreviousDiffChangeButton"));
                var nextChangeButton = Assert.IsType<Button>(
                    window.FindName("NextDiffChangeButton"));
                Assert.False(editor.IsReadOnly);
                Assert.Equal(
                    "HasSelectedSection",
                    BindingOperations.GetBinding(editor, UIElement.IsEnabledProperty)?.Path.Path);
                Assert.Equal(
                    "CanSave",
                    BindingOperations.GetBinding(saveButton, UIElement.IsEnabledProperty)?.Path.Path);
                Assert.Equal(
                    "SelectedDetailTabIndex",
                    BindingOperations.GetBinding(detailTabs, TabControl.SelectedIndexProperty)?.Path.Path);
                Assert.Equal(
                    "CanShowDiff",
                    BindingOperations.GetBinding(diffTab, UIElement.IsEnabledProperty)?.Path.Path);
                Assert.Equal(
                    "SelectedDiffLines",
                    BindingOperations.GetBinding(diffList, ItemsControl.ItemsSourceProperty)?.Path.Path);
                Assert.Equal(
                    "SelectedDiffLine",
                    BindingOperations.GetBinding(diffList, ListBox.SelectedItemProperty)?.Path.Path);
                Assert.Equal("Prev change", previousChangeButton.Content);
                Assert.Equal("Next change", nextChangeButton.Content);
                window.Close();
            }
            catch (Exception exception)
            {
                capturedException = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "The WPF smoke-test thread did not finish.");

        if (capturedException is not null)
        {
            ExceptionDispatchInfo.Capture(capturedException).Throw();
        }
    }
}
