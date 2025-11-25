using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

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
    private IntPtr _mainWindowHandle;

    public event EventHandler TargetWindowClosed;

    public FocusPullerService(WindowFinder windowFinder, IntPtr mainWindowHandle)
    {
        _windowFinder = windowFinder;
        _mainWindowHandle = mainWindowHandle;

        HwndSource source = HwndSource.FromHwnd(mainWindowHandle);
        source?.AddHook(WndProc);
     }

    public bool IsRunning => _isEnabled;
    public IntPtr TargetHandle => _targetWindowHandle;

    public void Start(int refocusDelayInMilliseconds)
    {
        RegisterHotKey();

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
        UnregisterHotKey();

        _isEnabled = false;
        _targetWindowHandle = IntPtr.Zero;
        _timer?.Stop();
        _timer = null;
    }

    public void UpdateDelay(int refocusDelayInMilliseconds)
    {
        _refocusDelayInMilliseconds = refocusDelayInMilliseconds;
    }

    public void RegisterHotKey()
    {
        const int MOD_CONTROL = 0x0002;
        const int MOD_ALT = 0x0001;
        const int MOD_SHIFT = 0x0004;
        const int VK_0 = 0x30; // '0' key
        const int HOTKEY_ID = 1;
        NativeMethods.RegisterHotKey(_mainWindowHandle, HOTKEY_ID, MOD_CONTROL | MOD_ALT | MOD_SHIFT, VK_0);
    }

    public void UnregisterHotKey()
    {
        const int HOTKEY_ID = 1;
        NativeMethods.UnregisterHotKey(_mainWindowHandle, HOTKEY_ID);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            BringTargetToForeground();
            handled = true;
        }

        return IntPtr.Zero;
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
                    // we will trigger the hotkey - this will then send back the WM_HOTKEY message which is picked up by the main window and
                    // sent back to us via NotifyHotKeyPressed, which then triggers the BringTargetToForeground method
                    // this gets around the focus-stealing measures in Windows (for now)
                    TriggerHotkey();
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

    private void BringTargetToForeground()
    {
        if (_isEnabled && _targetWindowHandle != IntPtr.Zero)
        {
            try
            {
                NativeMethods.SetForegroundWindow(_targetWindowHandle);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FocusPullerService: Exception occurred while handling hotkey invocation. {ex}");
            }
        }
    }

    private void TriggerHotkey()
    {
        const ushort VK_CONTROL = 0x11;
        const ushort VK_MENU = 0x12;    // ALT key
        const ushort VK_SHIFT = 0x10;
        const ushort VK_0 = 0x30;       // '0' key

        var inputs = new NativeMethods.INPUT[8];

        NativeMethods.INPUT MakeInput(ushort vk, uint flags = 0)
        {
            return new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                u = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = 0,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
        }

        // keys down
        inputs[0] = MakeInput(VK_CONTROL);
        inputs[1] = MakeInput(VK_MENU);
        inputs[2] = MakeInput(VK_SHIFT);
        inputs[3] = MakeInput(VK_0);

        // keys up
        inputs[4] = MakeInput(VK_0, NativeMethods.KEYEVENTF_KEYUP);
        inputs[5] = MakeInput(VK_SHIFT, NativeMethods.KEYEVENTF_KEYUP);
        inputs[6] = MakeInput(VK_MENU, NativeMethods.KEYEVENTF_KEYUP);
        inputs[7] = MakeInput(VK_CONTROL, NativeMethods.KEYEVENTF_KEYUP);

        uint result = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));

        if (result != inputs.Length)
        {
            // Handle error if needed
            int error = Marshal.GetLastWin32Error();
            System.Diagnostics.Debug.WriteLine($"SendInput failed. Sent {result} of {inputs.Length} inputs. Error: {error}");
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
}
