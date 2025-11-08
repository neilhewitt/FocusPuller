using System.Windows.Threading;
using FocusPuller.Models;

namespace FocusPuller.Services;

public class FocusPullerService
{
    private readonly WindowMonitor _windowMonitor;
    private readonly DispatcherTimer _timer;
    private IntPtr _targetWindowHandle;
    private int _refocusDelayMs;
    private bool _isEnabled;
    private DateTime _lastFocusLostTime;
    private bool _focusLost;

    public event EventHandler TargetWindowClosed;

    public FocusPullerService(WindowMonitor windowMonitor)
    {
        _windowMonitor = windowMonitor;
        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(500); // Check every 500ms
        _timer.Tick += Timer_Tick;
    }

    public void Start(IntPtr targetWindowHandle, int refocusDelayMs)
    {
        _targetWindowHandle = targetWindowHandle;
        _refocusDelayMs = refocusDelayMs;
        _isEnabled = true;
        _focusLost = false;
        _timer.Start();
    }

    public void Stop()
    {
        _isEnabled = false;
        _timer.Stop();
    }

    public void UpdateDelay(int refocusDelayMs)
    {
        _refocusDelayMs = refocusDelayMs;
    }

    public void UpdateTargetWindow(IntPtr targetWindowHandle)
    {
        _targetWindowHandle = targetWindowHandle;
        _focusLost = false;
    }

    private void Timer_Tick(object sender, EventArgs e)
    {
        if (!_isEnabled || _targetWindowHandle == IntPtr.Zero)
            return;

        // Check if target window still exists
        if (!Interop.NativeMethods.IsWindow(_targetWindowHandle))
        {
            TargetWindowClosed?.Invoke(this, EventArgs.Empty);
            Stop();
            return;
        }

        var foregroundWindow = _windowMonitor.GetForegroundWindow();

        if (foregroundWindow != _targetWindowHandle)
        {
            // Target window lost focus
            if (!_focusLost)
            {
                _focusLost = true;
                _lastFocusLostTime = DateTime.Now;
            }
            else
            {
                // Check if enough time has passed and user is idle
                var timeSinceFocusLost = (DateTime.Now - _lastFocusLostTime).TotalMilliseconds;
                var idleTime = _windowMonitor.GetIdleTimeMs();

                if (timeSinceFocusLost >= _refocusDelayMs && idleTime >= _refocusDelayMs)
                {
                    // Refocus the target window
                    _windowMonitor.SetForegroundWindow(_targetWindowHandle);
                    _focusLost = false;
                }
            }
        }
        else
        {
            // Target window has focus
            _focusLost = false;
        }
    }
}
