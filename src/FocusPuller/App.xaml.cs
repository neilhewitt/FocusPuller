using System.Configuration;
using System.Data;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using Hardcodet.Wpf.TaskbarNotification;

namespace FocusPuller;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private TaskbarIcon _trayIcon;
    private MainWindow _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Prefer the TaskbarIcon declared in App.xaml resources so its ContextMenu and handlers wired in XAML work
        if (this.Resources.Contains("TrayIcon") && this.Resources["TrayIcon"] is TaskbarIcon resourceIcon)
        {
            _trayIcon = resourceIcon;
        }
        else
        {
            // Fallback: create programmatically
            _trayIcon = new TaskbarIcon();
            _trayIcon.TrayMouseDoubleClick += TrayIcon_TrayMouseDoubleClick;
            _trayIcon.ToolTipText = "FocusPuller";

            var iconUri = new Uri("pack://application:,,,/focuspuller.ico", UriKind.Absolute);
            using (var stream = Application.GetResourceStream(iconUri)?.Stream)
            {
                if (stream != null)
                {
                    _trayIcon.Icon = new Icon(stream);
                }
            }
        }

        // Create main window
        _mainWindow = new MainWindow();
        _mainWindow.Show();
    }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // Handled in OnStartup
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        _trayIcon?.Dispose();
    }

    private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        _mainWindow?.RestoreFromTray();
    }

    private void MenuItem_Restore_Click(object sender, RoutedEventArgs e)
    {
        _mainWindow?.RestoreFromTray();
    }

    private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
    {
        _trayIcon?.Dispose();
        Shutdown();
    }
}

