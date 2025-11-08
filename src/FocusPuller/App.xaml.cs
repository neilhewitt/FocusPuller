using System.Configuration;
using System.Data;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using Hardcodet.Wpf.TaskbarNotification;
using FocusPuller.Services;

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
        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
        
        // Create a simple icon programmatically
        _trayIcon.Icon = CreateTrayIcon();

        // Load settings to check hide mode
        var settingsManager = new SettingsManager();
        var settings = settingsManager.LoadSettings();

        // Create main window
        _mainWindow = new MainWindow();

        if (settings.IsHideMode)
        {
            // Start minimized to tray with refocusing on
            // Don't show the window
        }
        else
        {
            // Start normally - show window, refocusing off
            _mainWindow.Show();
        }
    }

    private Icon CreateTrayIcon()
    {
        // Create a simple icon with a colored circle
        const int size = 16;
        using (Bitmap bitmap = new Bitmap(size, size))
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            
            // Fill with transparent background
            g.Clear(Color.Transparent);
            
            // Draw a blue circle
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(0, 120, 215))) // Windows blue
            {
                g.FillEllipse(brush, 2, 2, size - 4, size - 4);
            }
            
            // Draw white border
            using (Pen pen = new Pen(Color.White, 1.5f))
            {
                g.DrawEllipse(pen, 2, 2, size - 4, size - 4);
            }
            
            // Convert bitmap to icon
            IntPtr hIcon = bitmap.GetHicon();
            Icon icon = Icon.FromHandle(hIcon);
            return icon;
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

