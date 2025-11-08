using System.Configuration;
using System.Data;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using Hardcodet.Wpf.TaskbarNotification;
using FocusPuller.Services;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using FocusPuller.Interop;

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

        // Initialize tray icon
        _trayIcon = new TaskbarIcon();
        _trayIcon.TrayMouseDoubleClick += TrayIcon_TrayMouseDoubleClick;    
        
        // Set tooltip for tray icon
        _trayIcon.ToolTipText = "FocusPuller";

        // Set tray icon from focuspuller.ico resource
        var iconUri = new Uri("pack://application:,,,/focuspuller.ico", UriKind.Absolute);
        using (var stream = Application.GetResourceStream(iconUri)?.Stream)
        {
            if (stream != null)
            {
                _trayIcon.Icon = new Icon(stream);
            }
        }

        // Load settings to check hide mode
        var settingsManager = new SettingsManager();
        var settings = settingsManager.LoadSettings();

        // Create main window
        _mainWindow = new MainWindow();

        if (settings.IsHideMode)
        {
            // Show the window initially, then minimize to tray
            _mainWindow.Show();

            // Minimize after layout so user briefly sees UI before it goes to tray
            _mainWindow.Dispatcher.BeginInvoke(new Action(() => {
                _mainWindow.WindowState = WindowState.Minimized;
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
        else
        {
            // Start normally - show window, refocusing off
            _mainWindow.Show();
        }
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

