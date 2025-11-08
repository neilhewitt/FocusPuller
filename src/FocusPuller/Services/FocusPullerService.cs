using System.Windows.Threading;
using FocusPuller.Models;
using FocusPuller.Interop;
using System.Runtime.InteropServices;
using System.Drawing;

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
                    try
                    {
                        // Restore target window (in case minimized)
                        NativeMethods.ShowWindow(_targetWindowHandle, NativeMethods.SW_RESTORE);

                        // Determine whether the window is already topmost
                        var exStylePtr = NativeMethods.GetWindowLongPtr(_targetWindowHandle, NativeMethods.GWL_EXSTYLE);
                        bool wasTopMost = (exStylePtr.ToInt64() & NativeMethods.WS_EX_TOPMOST) != 0;

                        // If not topmost, temporarily set it topmost so a click will bring it to foreground
                        bool madeTopMost = false;
                        if (!wasTopMost)
                        {
                            madeTopMost = NativeMethods.SetWindowPos(_targetWindowHandle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
                        }

                        // Simulate a click in the center of the target window
                        if (NativeMethods.GetWindowRect(_targetWindowHandle, out var rect))
                        {
                            int centerX = rect.Left + rect.Width / 2;
                            int centerY = rect.Top + rect.Height / 2;

                            // Save current cursor position
                            NativeMethods.GetCursorPos(out var originalPos);

                            // Move cursor and click
                            NativeMethods.SetCursorPos(centerX, centerY);
                            NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTDOWN, (uint)centerX, (uint)centerY, 0, UIntPtr.Zero);
                            NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTUP, (uint)centerX, (uint)centerY, 0, UIntPtr.Zero);

                            // Restore cursor
                            NativeMethods.SetCursorPos(originalPos.X, originalPos.Y);
                        }

                        // If we made it topmost earlier, unset topmost
                        if (!wasTopMost && madeTopMost)
                        {
                            NativeMethods.SetWindowPos(_targetWindowHandle, NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0,
                                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
                        }

                        // Give this process permission to set foreground and then set foreground
                        var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                        NativeMethods.AllowSetForegroundWindow(new IntPtr(pid));
                        _windowMonitor.SetForegroundWindow(_targetWindowHandle);

                        _focusLost = false;
                    }
                    catch
                    {
                        // Swallow exceptions to avoid crashing timer thread
                    }
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
