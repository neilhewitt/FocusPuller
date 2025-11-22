using System.Runtime.InteropServices;
using System.Windows.Threading;
using System.Diagnostics;

namespace FocusPuller;

public class FocusPullerService
{
    private WindowFinder _windowFinder;
    private DispatcherTimer _timer;
    private IntPtr _targetWindowHandle;
    private int _refocusDelayInMilliseconds;
    private bool _isEnabled;
    private DateTime _lastFocusLostTime;
    private bool _focusLost;

    public event EventHandler TargetWindowClosed;

    public FocusPullerService(WindowFinder windowFinder)
    {
        _windowFinder = windowFinder;
    }

    public bool IsRunning => _isEnabled;
    public IntPtr TargetHandle => _targetWindowHandle;

    public void Start(int refocusDelayInMilliseconds)
    {
        _refocusDelayInMilliseconds = refocusDelayInMilliseconds;
        _isEnabled = true;
        _focusLost = false;
        
        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(500); // Check every 500ms
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    public void Stop()
    {
        _isEnabled = false;
        _targetWindowHandle = IntPtr.Zero;
        _timer?.Stop();
        _timer = null;
    }

    public void UpdateDelay(int refocusDelayInMilliseconds)
    {
        _refocusDelayInMilliseconds = refocusDelayInMilliseconds;
    }

    private void Timer_Tick(object sender, EventArgs e)
    {
        if (!_isEnabled)
        {
            return;
        }

        // If we don't have a handle try to find the right window
        if (_targetWindowHandle == IntPtr.Zero)
        {
            var targetWindow = _windowFinder.FindTargetWindow(); // finds an open target based on the rules
            if (targetWindow != null)
            {
                _targetWindowHandle = targetWindow.Handle;
            }
            else
            {
                return;
            }
        }

        // Check if target window still exists
        if (!NativeMethods.IsWindow(_targetWindowHandle))
        {
            TargetWindowClosed?.Invoke(this, EventArgs.Empty);
            Stop();
            return;
        }

        var foregroundWindow = NativeMethods.GetForegroundWindow();
        if (foregroundWindow != _targetWindowHandle)
        {
            // Target window lost focus
            if (!_focusLost)
            {
                _focusLost = true;
                _lastFocusLostTime = DateTime.UtcNow;
            }
            else
            {
                // Check if enough time has passed and user is idle
                var timeSinceFocusLost = (DateTime.UtcNow - _lastFocusLostTime).TotalMilliseconds;
                var idleTime = GetIdleTime();

                if (timeSinceFocusLost >= _refocusDelayInMilliseconds && idleTime >= _refocusDelayInMilliseconds)
                {
                    try
                    {
                        // Restore target window (in case minimized)
                        NativeMethods.ShowWindow(_targetWindowHandle, NativeMethods.SW_RESTORE);

                        // set it topmost temporarily to force focus
                        if (NativeMethods.SetWindowPos(_targetWindowHandle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE))
                        {

                            // Simulate a click in the horizontal center of the target window's title bar / top area
                            if (NativeMethods.GetWindowRect(_targetWindowHandle, out var rect))
                            {
                                int centerX = rect.Left + rect.Width / 2;
                                int centerY = GetTitleBarClickY(rect, _targetWindowHandle);

                                // Save current cursor position
                                NativeMethods.GetCursorPos(out var originalPos);

                                // Move cursor and click
                                NativeMethods.SetCursorPos(centerX, centerY);
                                NativeMethods.SendMouseEvent(NativeMethods.MOUSEEVENTF_LEFTDOWN, (uint)centerX, (uint)centerY, 0, UIntPtr.Zero);
                                NativeMethods.SendMouseEvent(NativeMethods.MOUSEEVENTF_LEFTUP, (uint)centerX, (uint)centerY, 0, UIntPtr.Zero);

                                // Restore cursor
                                NativeMethods.SetCursorPos(originalPos.X, originalPos.Y);
                            }

                            // unset topmost - if it was already topmost this will break that, but otherwise a window can become
                            // 'trapped' as topmost, causing issues
                            NativeMethods.SetWindowPos(_targetWindowHandle, NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0,
                                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
                        }
                        else
                        {
                            // just try to set foreground the traditional way, which might fail due to OS restrictions
                            NativeMethods.SetForegroundWindow(_targetWindowHandle);
                        }

                        _focusLost = false;
                    }
                    catch (Exception ex)
                    {
                        // Swallow exceptions to avoid crashing timer thread, but log to the debug output
                        Debug.WriteLine($"FocusPullerService: Exception occurred while trying to refocus target window. {ex}");
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

    private uint GetIdleTime()
    {
        var lastInputInfo = new NativeMethods.LASTINPUTINFO();
        lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);

        if (NativeMethods.GetLastInputInfo(ref lastInputInfo))
        {
            uint tick = NativeMethods.GetTickCount();
            return tick - lastInputInfo.dwTime;
        }

        return 0;
    }

    private int GetTitleBarClickY(NativeMethods.RECT rect, IntPtr hWnd)
    {
        // Default fallback: click 1/12th down from top or at least 8px
        int fallback = rect.Top + Math.Max(8, rect.Height / 12);

        try
        {
            // Determine if window style includes a caption/title bar
            var stylePtr = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_STYLE);
            bool hasCaption = (stylePtr.ToInt64() & NativeMethods.WS_CAPTION) != 0;

            int captionHeight = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYCAPTION);
            int frameHeight = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYFRAME);

            if (hasCaption)
            {
                // Titlebar height includes caption + frame
                int titleBarHeight = captionHeight + frameHeight;
                return rect.Top + titleBarHeight / 2;
            }
            else
            {
                // No caption - click near the top edge within a small area
                int topArea = Math.Max(8, Math.Min(rect.Height / 12, 48));
                return rect.Top + topArea / 2;
            }
        }
        catch
        {
            return fallback;
        }
    }
}
