using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace FocusPuller;

public class FocusPullerService
{
    private readonly WindowFinder _windowFinder;
    private readonly DispatcherTimer _timer;
    private IntPtr _targetWindowHandle;
    private string? _targetClassName;
    private string? _targetTitlePrefix;
    private int _refocusDelayInMilliseconds;
    private bool _isEnabled;
    private DateTime _lastFocusLostTime;
    private bool _focusLost;

    public event EventHandler TargetWindowClosed;

    public FocusPullerService(WindowFinder windowFinder)
    {
        _windowFinder = windowFinder;
        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(500); // Check every 500ms
        _timer.Tick += Timer_Tick;
    }

    public bool IsRunning => _isEnabled;
    public IntPtr TargetHandle => _targetWindowHandle;

    public void Start(int refocusDelayInMilliseconds, string targetClassName, string targetTitlePrefix)
    {
        _targetClassName = targetClassName;
        _targetTitlePrefix = targetTitlePrefix;
        _refocusDelayInMilliseconds = refocusDelayInMilliseconds;
        _isEnabled = true;
        _focusLost = false;
        _timer.Start();
    }

    public void Stop()
    {
        _isEnabled = false;
        _timer.Stop();
    }

    public void UpdateDelay(int refocusDelayInMilliseconds)
    {
        _refocusDelayInMilliseconds = refocusDelayInMilliseconds;
    }

    public void UpdateTargetWindow(IntPtr targetWindowHandle)
    {
        _targetWindowHandle = targetWindowHandle;
        _focusLost = false;
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
                // Click halfway through the title bar area
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

    private void Timer_Tick(object sender, EventArgs e)
    {
        if (!_isEnabled)
        {
            return;
        }

        // If we don't have a handle but have class/title info, try to find the window
        if (_targetWindowHandle == IntPtr.Zero && !string.IsNullOrEmpty(_targetTitlePrefix))
        {
            var targetWindow = _windowFinder.FindTargetWindow();
            if (targetWindow != null)
            {
                _targetWindowHandle = targetWindow.Handle;
            }
        }
        else if (_targetWindowHandle == IntPtr.Zero)
        {
            return;
        }

        // Check if target window still exists
        if (!NativeMethods.IsWindow(_targetWindowHandle))
        {
            TargetWindowClosed?.Invoke(this, EventArgs.Empty);
            Stop();
            return;
        }

        var foregroundWindow = GetForegroundWindow();
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
                var idleTime = GetIdleTime();

                if (timeSinceFocusLost >= _refocusDelayInMilliseconds && idleTime >= _refocusDelayInMilliseconds)
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

                        // Simulate a click in the horizontal center of the target window's title bar / top area
                        if (NativeMethods.GetWindowRect(_targetWindowHandle, out var rect))
                        {
                            int centerX = rect.Left + rect.Width / 2;
                            int centerY = GetTitleBarClickY(rect, _targetWindowHandle);

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
                        SetForegroundWindow(_targetWindowHandle);

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

    private IntPtr GetForegroundWindow()
    {
        return NativeMethods.GetForegroundWindow();
    }

    private bool SetForegroundWindow(IntPtr hWnd)
    {
        if (!NativeMethods.IsWindow(hWnd))
            return false;

        // Only set foreground if not already focused
        if (NativeMethods.GetForegroundWindow() == hWnd)
            return true;

        // Focus window
        //NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
        bool focused = NativeMethods.SetForegroundWindow(hWnd);

        // set the cursor position to the center of the window
        NativeMethods.GetWindowRect(hWnd, out var rect);
        int centerX = (rect.Left + rect.Right) / 2;
        int centerY = (rect.Top + rect.Bottom) / 2;
        NativeMethods.SetCursorPos(centerX, centerY);

        return focused;
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

}
