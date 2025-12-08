using System.Configuration;
using System.Data;
using System.Drawing;
using System.IO.Pipes;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using Hardcodet.Wpf.TaskbarNotification;

namespace FocusPuller;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private const string MutexName = "FocusPuller_SingleInstance_Mutex";
    private const string PipeName = "FocusPuller_SingleInstance_Pipe";
    
    private Mutex _singleInstanceMutex;
    private TaskbarIcon _trayIcon;
    private MainWindow _mainWindow;
    private NamedPipeServerStream _pipeServer;
    private CancellationTokenSource _pipeListenerCancellation;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Check if another instance is already running
        bool createdNew;
        _singleInstanceMutex = new Mutex(true, MutexName, out createdNew);

        if (!createdNew)
        {
            // Another instance is running, signal it to show and exit this instance
            SignalFirstInstance();
            Shutdown();
            return;
        }

        try
        {
            // Start listening for signals from other instances
            StartPipeServer();

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
        catch (Exception ex)
        {
            MessageBox.Show("An error occurred during application startup:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // Handled in OnStartup
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        
        // Clean up single-instance resources
        _pipeListenerCancellation?.Cancel();
        _pipeServer?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
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

    private void StartPipeServer()
    {
        _pipeListenerCancellation = new CancellationTokenSource();
        Task.Run(async () =>
        {
            while (!_pipeListenerCancellation.Token.IsCancellationRequested)
            {
                try
                {
                    _pipeServer = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
                    await _pipeServer.WaitForConnectionAsync(_pipeListenerCancellation.Token);

                    // Signal received, restore the main window
                    Dispatcher.Invoke(() =>
                    {
                        _mainWindow?.RestoreFromTray();
                    });

                    _pipeServer.Dispose();
                }
                catch (OperationCanceledException)
                {
                    // Expected when shutting down
                    break;
                }
                catch (Exception)
                {
                    // Ignore pipe errors and continue listening
                }
            }
        }, _pipeListenerCancellation.Token);
    }

    private void SignalFirstInstance()
    {
        try
        {
            using (var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
            {
                pipeClient.Connect(1000); // Wait up to 1 second
            }
        }
        catch (Exception)
        {
            // If we can't signal the first instance, just exit silently
        }
    }
}

