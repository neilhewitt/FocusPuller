using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation.Text;
using System.Windows.Interop;
using System.Windows.Threading;

namespace FocusPuller;

public class FocusPullerService
{
    private WindowFinder _windowFinder;
    private Settings _settings;
    private DispatcherTimer _timer;
    private IntPtr _targetWindowHandle;
    private WindowInfo _targetWindow;
    private int _refocusDelayInMilliseconds;
    private DateTime _lastFocusLostTime;
    private bool _focusLost;
    private IntPtr _mainWindowHandle;
    private const int HOTKEY_ID = 1;

    private bool _refocusing;

    public event EventHandler TargetWindowClosed;
    public event EventHandler<WindowInfo> TargetWindowCreated;

    public WindowInfo TargetWindow => _targetWindow;

    public FocusPullerService(WindowFinder windowFinder, Settings settings, IntPtr mainWindowHandle)
    {
        _windowFinder = windowFinder;
        _settings = settings;
        _mainWindowHandle = mainWindowHandle;

        HwndSource source = HwndSource.FromHwnd(mainWindowHandle);
        source?.AddHook(WndProc);
     }

    public void Start(int refocusDelayInMilliseconds)
    {
        RegisterHotKey();

        _refocusDelayInMilliseconds = refocusDelayInMilliseconds;
        _focusLost = false;

        Timer_Tick(this, EventArgs.Empty); // Initial check

        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(500); // Check every 500ms
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    public void Stop()
    {
        UnregisterHotKey();

        if (_targetWindowHandle != IntPtr.Zero)
        {
            TargetWindowClosed?.Invoke(this, EventArgs.Empty);
        }

        _targetWindowHandle = IntPtr.Zero;
        _timer?.Stop();
        _timer = null;
    }

    public void BeginRefocusing()
    {
        _refocusing = true;
    }

    public void EndRefocusing()
    {
        _refocusing = false;
    }

    public void UpdateDelay(int refocusDelayInMilliseconds)
    {
        _refocusDelayInMilliseconds = refocusDelayInMilliseconds;
    }

    public void UpdateHotkey()
    {
        // Re-register the hotkey with new settings
        UnregisterHotKey();
        RegisterHotKey();
    }

    private void RegisterHotKey()
    {
        if (!_settings.Values.HasValidHotkey())
        {
            Debug.WriteLine("Cannot register hotkey: invalid configuration");
            return;
        }

        (_, _, _, uint modifiers, uint vk, _) = _settings.Values.GetHotkeyInfo();        
        bool success = Native.RegisterHotKey(_mainWindowHandle, HOTKEY_ID, modifiers, vk);
        
        if (!success)
        {
            int error = Marshal.GetLastWin32Error();
            Debug.WriteLine($"Failed to register hotkey. Error: {error}");
        }
    }

    private void UnregisterHotKey()
    {
        Native.UnregisterHotKey(_mainWindowHandle, HOTKEY_ID);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY)
        {
            BringTargetToForeground();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void Timer_Tick(object sender, EventArgs e)
    {
        // If we don't have a handle try to find the right window
        if (_targetWindowHandle == IntPtr.Zero)
        {
            _targetWindow = _windowFinder.FindTargetWindow(); // finds an open target based on the rules
            if (_targetWindow != null)
            {
                _targetWindowHandle = _targetWindow.Handle;
                TargetWindowCreated?.Invoke(this, _targetWindow);
            }
            else
            {
                return;
            }
        }

        // Check if target window still exists
        if (!Native.IsWindow(_targetWindowHandle))
        {
            _targetWindow = null;
            _targetWindowHandle = IntPtr.Zero;
            TargetWindowClosed?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (_refocusing)
        {
            var foregroundWindow = Native.GetForegroundWindow();
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
    }

    private void BringTargetToForeground()
    {
        if (_refocusing && _targetWindowHandle != IntPtr.Zero)
        {
            try
            {
                Native.SetForegroundWindow(_targetWindowHandle);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FocusPullerService: Exception occurred while handling hotkey invocation. {ex}");
            }
        }
    }

    private void TriggerHotkey()
    {
        ushort controlKey = (ushort)VirtualKey.ControlKey;
        ushort altKey = (ushort)VirtualKey.AltKey;
        ushort shiftKey = (ushort)VirtualKey.ShiftKey;

        var hotkeyInfo = _settings.Values.GetHotkeyInfo();

        var inputCount = 0;
        var inputs = new Native.INPUT[hotkeyInfo.keyCount * 2]; // Max needed: 4 down + 4 up

        // press modifier keys in order
        AddInput(hotkeyInfo.useControl, controlKey);
        AddInput(hotkeyInfo.useAlt, altKey);
        AddInput(hotkeyInfo.useShift, shiftKey);
        AddInput(true, (ushort)hotkeyInfo.virtualKeyCode);

        // release keys in reverse order
        AddInput(true, (ushort)hotkeyInfo.virtualKeyCode, Native.KEYEVENTF_KEYUP);
        AddInput(hotkeyInfo.useShift, shiftKey, Native.KEYEVENTF_KEYUP);
        AddInput(hotkeyInfo.useAlt, altKey, Native.KEYEVENTF_KEYUP);
        AddInput(hotkeyInfo.useControl, controlKey, Native.KEYEVENTF_KEYUP);

        uint result = Native.SendInput((uint)inputCount, inputs, Marshal.SizeOf(typeof(Native.INPUT)));

        if (result != inputCount)
        {
            int error = Marshal.GetLastWin32Error();
            Debug.WriteLine($"SendInput failed. Sent {result} of {inputCount} inputs. Error: {error}");
        }

        void AddInput(bool add, ushort vk, uint flags = 0)
        {
            if (add)
            {
                inputs[inputCount++] = new Native.INPUT
                {
                    type = Native.INPUT_KEYBOARD,
                    u = new Native.InputUnion
                    {
                        ki = new Native.KEYBDINPUT
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
        }
    }

    private uint GetIdleTime()
    {
        var lastInputInfo = new Native.LASTINPUTINFO();
        lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);

        if (Native.GetLastInputInfo(ref lastInputInfo))
        {
            uint tick = Native.GetTickCount();
            return tick - lastInputInfo.dwTime;
        }

        return 0;
    }
}
