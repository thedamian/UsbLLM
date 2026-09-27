using System.Windows;
using System.Windows.Threading;

namespace UsbLlm;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var splash = new SplashWindow();
        splash.Show();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1250) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
            splash.Close();
        };
        timer.Start();
    }
}
