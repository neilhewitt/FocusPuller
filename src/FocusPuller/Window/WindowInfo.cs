using System.ComponentModel;

namespace FocusPuller;

public class WindowInfo : INotifyPropertyChanged
{
    public IntPtr Handle { get; set; }
    
    private string _title;
    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }
    
    public string ClassName { get; set; }
    public string DisplayName => $"{Title} [{ClassName}]";

    private bool _exists = true;
    public bool Exists
    {
        get => _exists;
        set
        {
            if (_exists != value)
            {
                _exists = value;
                OnPropertyChanged(nameof(Exists));
            }
        }
    }

    public WindowInfo(IntPtr handle, string title, string className)
    {
        Handle = handle;
        _title = title;
        ClassName = className;
    }

    public override string ToString() => DisplayName;

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
