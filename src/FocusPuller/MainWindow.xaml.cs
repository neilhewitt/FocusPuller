using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace FocusPuller;

public partial class MainWindow : Window
{
    private const string NO_WINDOW_SELECTED = "No target window selected";
    
    private FocusPullerService _focusPullerService;
    private Settings _settings;
    private WindowFinder _windowFinder;
    private WindowInfo _targetWindow;
    private bool _isRefocusing = false;
    private System.Windows.Threading.DispatcherTimer _windowCheckTimer;
    private System.Windows.Threading.DispatcherTimer _windowListRefreshTimer;

    public MainWindow()
    {
        InitializeComponent();

        _settings = new Settings();
        _settings.Load();

        _windowFinder = new WindowFinder(_settings);

        _focusPullerService = new FocusPullerService(_windowFinder);
        _focusPullerService.TargetWindowClosed += FocusPullerService_TargetWindowClosed;

        _windowCheckTimer = new System.Windows.Threading.DispatcherTimer();
        _windowCheckTimer.Interval = TimeSpan.FromMilliseconds(500);
        _windowCheckTimer.Tick += WindowCheckTimer_Tick;
        _windowCheckTimer.Start();

        _windowListRefreshTimer = new System.Windows.Threading.DispatcherTimer();
        _windowListRefreshTimer.Interval = TimeSpan.FromSeconds(3);
        _windowListRefreshTimer.Tick += WindowListRefreshTimer_Tick;
        _windowListRefreshTimer.Start();

        Initialise();

        if (_settings.Values.IsHideMode)
        {
            MinimiseToTray();   
        }
    }

    private void Initialise()
    {
        DelaySlider.Value = _settings.Values.RefocusDelayInMilliseconds;
        HideModeCheckBox.IsChecked = _settings.Values.IsHideMode;
        _isRefocusing = HideModeCheckBox.IsChecked ?? false; // switch on if Hide Mode is enabled

        _targetWindow = _windowFinder.FindTargetWindow();
        WindowStatusLabel.Text = _targetWindow?.Title ?? NO_WINDOW_SELECTED;
        
        if (_settings.Values.IsHideMode && _targetWindow != null)
        {
            StartRefocusing();
        }

        UpdateRefocusingButton();
    }

    private void SaveSettings()
    {
        _settings.Values.RefocusDelayInMilliseconds = (int)DelaySlider.Value;
        _settings.Values.IsHideMode = HideModeCheckBox.IsChecked ?? false;
        _settings.Values.MatchingRules = _windowFinder.Rules;
        _settings.Values.TargetWindowTitle = _targetWindow?.Title ?? "";
        _settings.Values.TargetWindowClassName = _targetWindow?.ClassName ?? "";

        if (_targetWindow != null)
        {
            // If the selected window matches a rule, save the rule prefix instead of the full title
            var rule = _windowFinder.FindMatchingRule(_targetWindow.ClassName, _targetWindow.Title);
            if (rule != null)
            {
                // Find the first prefix that matches the window title
                var matchingPrefix = rule.TitlePrefixes.FirstOrDefault(p =>
                    !string.IsNullOrEmpty(p) && _targetWindow.Title != null && _targetWindow.Title.StartsWith(p, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(matchingPrefix))
                {
                    _settings.Values.TargetWindowTitle = matchingPrefix;
                }
            }
        }

        _settings.SaveSettings(_settings);
    }

    private void StartRefocusing()
    {
        // Only show error if no window is selected at all (not even a placeholder)
        if (_targetWindow == null)
        {
            MessageBox.Show("Please select a target window first.", "No Target Window",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            _isRefocusing = false;
            UpdateRefocusingButton();
            return;
        }

        _focusPullerService.Start((int)DelaySlider.Value, _targetWindow.ClassName, _targetWindow.Title);
        
        _isRefocusing = true;
        UpdateRefocusingButton();
        SaveSettings();
    }

    private void StopRefocusing()
    {
        _focusPullerService.Stop();
        _isRefocusing = false;
        UpdateRefocusingButton();
        SaveSettings();
    }

    private void UpdateRefocusingButton()
    {
        if (_isRefocusing)
        {
            RefocusingButton.Content = "On";
            RefocusingButton.Background = new SolidColorBrush(Colors.Green);
        }
        else
        {
            RefocusingButton.Content = "Off";
            RefocusingButton.Background = new SolidColorBrush(Colors.Red);
        }

        // Enable the refocus button only if a target window is selected.
        RefocusingButton.IsEnabled = _targetWindow != null && (_targetWindow.Exists || _isRefocusing);
    }

    private void UpdateWindowStatus()
    {
        // If no window is selected, clear the label
        if (_targetWindow == null)
        {
            WindowStatusLabel.Text = NO_WINDOW_SELECTED;
            UpdateRefocusingButton();
            return;
        }
        
        if (!_windowFinder.IsVisible(_targetWindow))
        {
            WindowStatusLabel.Text = "Target window not open / available";
            WindowStatusLabel.Foreground = new SolidColorBrush(Colors.Red);
            _targetWindow.Exists = false;
            
            if (_isRefocusing)
            {
                _focusPullerService.Stop();
            }
        }
        else
        {
            // Window exists
            WindowStatusLabel.Text = _targetWindow.Title;
            WindowStatusLabel.Foreground = new SolidColorBrush(Colors.Black);
            _targetWindow.Exists = true;
        }
        
        UpdateRefocusingButton();
    }

    private void WindowListRefreshTimer_Tick(object sender, EventArgs e)
    {
        UpdateWindowStatus();
    }

    private void WindowCheckTimer_Tick(object sender, EventArgs e)
    {
        UpdateWindowStatus();
    }

    private void DelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_focusPullerService != null && _isRefocusing)
        {
            _focusPullerService.UpdateDelay((int)e.NewValue);
            SaveSettings();
        }
    }

    private void DelayTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        var textBox = (System.Windows.Controls.TextBox)sender;

        // Remove any non-digit characters
        string text = textBox.Text;
        string digitsOnly = new string(text.Where(char.IsDigit).ToArray());

        if (text != digitsOnly && text != "")
        {
            int caretIndex = textBox.CaretIndex;
            textBox.Text = digitsOnly;
            textBox.CaretIndex = Math.Min(caretIndex, digitsOnly.Length);
        }

        if (text != "" && int.TryParse(digitsOnly, out int value))
        {
            // Clamp the value between min and max
            if (value < DelaySlider.Minimum)
            {
                value = (int)DelaySlider.Minimum;
            }
            else if (value > DelaySlider.Maximum)
            {
                value = (int)DelaySlider.Maximum;
            }

            // Only update if different to avoid infinite loop
            if (Math.Abs(DelaySlider.Value - value) > 0.5)
            {
                DelaySlider.Value = value;
            }
        }

        if (text == "")
        {
            // Reset to slider value if empty
            textBox.Text = ((int)DelaySlider.Value).ToString();
        }
    }

    private void DelayTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Allow Enter key to commit the value
        if (e.Key == Key.Enter)
        {
            // Force the binding to update
            var textBox = (System.Windows.Controls.TextBox)sender;
            var bindingExpression = textBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
            bindingExpression?.UpdateSource();
            
            // Move focus away to trigger validation
            Keyboard.ClearFocus();
            e.Handled = true;
            return;
        }

        // Allow navigation and editing keys
        if (e.Key == Key.Back || e.Key == Key.Delete || 
            e.Key == Key.Left || e.Key == Key.Right || 
            e.Key == Key.Home || e.Key == Key.End ||
            e.Key == Key.Tab)
        {
            return;
        }

        // Allow number keys from main keyboard (D0-D9)
        if (e.Key >= Key.D0 && e.Key <= Key.D9)
        {
            return;
        }

        // Allow number keys from numpad (NumPad0-NumPad9)
        if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
        {
            return;
        }

        // Block all other keys
        e.Handled = true;
    }

    private void RefocusingButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefocusing)
        {
            StopRefocusing();
        }
        else
        {
            StartRefocusing();
        }
    }

    private void HideModeCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        // If Hide Mode is being enabled and we have a selected window, also enable refocusing
        if (HideModeCheckBox.IsChecked == true && _targetWindow != null && !_isRefocusing)
        {
            StartRefocusing();
        }
        
        SaveSettings();
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    public void MinimiseToTray()
    {
        WindowState = WindowState.Minimized;
    }

    public void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void FocusPullerService_TargetWindowClosed(object sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateRefocusingButton();
        });
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        // Stop background timers and services
        _windowCheckTimer?.Stop();
        _windowListRefreshTimer?.Stop();
        _focusPullerService.Stop();
        SaveSettings();
    }
}