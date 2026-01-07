using System.ComponentModel;
using System.Text;

namespace FocusPuller;

public class WindowInfo
{
    public IntPtr Handle { get; set; }

    public string Title { get; set; }
    public string ClassName { get; set; }
    public string DisplayName => $"{Title} [{ClassName}]";

    public WindowInfo(IntPtr handle, string title, string className)
    {
        Handle = handle;
        Title = title;
        ClassName = className;
    }

    public override string ToString() => DisplayName;
}
