using System.Runtime.ExceptionServices;
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
