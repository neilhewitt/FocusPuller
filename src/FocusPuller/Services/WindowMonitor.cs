using System.Runtime.InteropServices;
using System.Text;
using FocusPuller.Interop;
using FocusPuller.Models;

namespace FocusPuller.Services;

public class WindowMonitor
{
    public List<WindowInfo> GetVisibleWindows()
    {
        var windows = new List<WindowInfo>();

        NativeMethods.EnumWindows((hWnd, lParam) =>
        {
            if (NativeMethods.IsWindowVisible(hWnd))
            {
                var titleBuilder = new StringBuilder(256);
                var classBuilder = new StringBuilder(256);
                
                NativeMethods.GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                NativeMethods.GetClassName(hWnd, classBuilder, classBuilder.Capacity);

                var title = titleBuilder.ToString();
                var className = classBuilder.ToString();

                // Only include windows with titles
                if (!string.IsNullOrWhiteSpace(title))
                {
                    windows.Add(new WindowInfo(hWnd, title, className));
                }
            }

            return true; // Continue enumeration
        }, IntPtr.Zero);

        return windows;
    }

    public IntPtr GetForegroundWindow()
    {
        return NativeMethods.GetForegroundWindow();
    }

    public bool SetForegroundWindow(IntPtr hWnd)
    {
        if (!NativeMethods.IsWindow(hWnd))
            return false;

        // Restore window if minimized
        NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
        
        return NativeMethods.SetForegroundWindow(hWnd);
    }

    public uint GetIdleTimeMs()
    {
        var lastInputInfo = new NativeMethods.LASTINPUTINFO();
        lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
        
        if (NativeMethods.GetLastInputInfo(ref lastInputInfo) != IntPtr.Zero)
        {
            return NativeMethods.GetTickCount() - lastInputInfo.dwTime;
        }

        return 0;
    }

    public WindowInfo GetWindowInfo(IntPtr hWnd)
    {
        var titleBuilder = new StringBuilder(256);
        var classBuilder = new StringBuilder(256);
        
        NativeMethods.GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
        NativeMethods.GetClassName(hWnd, classBuilder, classBuilder.Capacity);

        return new WindowInfo(hWnd, titleBuilder.ToString(), classBuilder.ToString());
    }
}
