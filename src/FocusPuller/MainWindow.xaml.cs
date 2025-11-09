using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FocusPuller.Models;
using FocusPuller.Services;

namespace FocusPuller;

public partial class MainWindow : Window
{
    private readonly WindowMonitor _windowMonitor;
    private readonly FocusPullerService _focusPullerService;
    private readonly SettingsManager _settingsManager;
    private readonly WindowMatchingService _windowMatchingService;
    private AppSettings _settings;
    private WindowInfo _selectedWindow;
    private bool _isRefocusing = false;
    private System.Windows.Threading.DispatcherTimer _windowCheckTimer;
    private System.Windows.Threading.DispatcherTimer _windowListRefreshTimer;

    public MainWindow()
    {
        InitializeComponent();

        _windowMonitor = new WindowMonitor();
        _focusPullerService = new FocusPullerService(_windowMonitor);
        _settingsManager = new SettingsManager();
        _windowMatchingService = new WindowMatchingService();

        _focusPullerService.TargetWindowClosed += FocusPullerService_TargetWindowClosed;

        // Set up timer to periodically check if the target window exists
        _windowCheckTimer = new System.Windows.Threading.DispatcherTimer();
        _windowCheckTimer.Interval = TimeSpan.FromMilliseconds(500);
        _windowCheckTimer.Tick += WindowCheckTimer_Tick;
        _windowCheckTimer.Start();

        // Set up timer to refresh window list periodically (every 3 seconds)
        _windowListRefreshTimer = new System.Windows.Threading.DispatcherTimer();
        _windowListRefreshTimer.Interval = TimeSpan.FromSeconds(3);
        _windowListRefreshTimer.Tick += WindowListRefreshTimer_Tick;
        _windowListRefreshTimer.Start();

        RefreshWindowList();
        LoadSettings();
    }

    private void WindowListRefreshTimer_Tick(object sender, EventArgs e)
    {
        RefreshWindowList();
        UpdateWindowStatus();
    }

    private void LoadSettings()
    {
        _settings = _settingsManager.LoadSettings();

        // Load matching rules into the service
        _windowMatchingService.LoadRules(_settings.MatchingRules);

        //// If no rules were loaded (first run), save the default rules
        //if (_settings.MatchingRules == null || _settings.MatchingRules.Count == 0)
        //{
        //    _settings.MatchingRules = _windowMatchingService.GetRulesData();
        //    _settingsManager.SaveSettings(_settings);
        //}

        DelaySlider.Value = _settings.RefocusDelayMs;
        HideModeCheckBox.IsChecked = _settings.IsHideMode;

        // Set refocusing state based on Hide Mode
        // If Hide Mode is on, start with refocusing enabled; otherwise start with it off
        _isRefocusing = _settings.IsHideMode;

        bool found = !string.IsNullOrWhiteSpace(_settings.TargetWindowTitle);
        // If no target defined and the user only allows rule-defined windows, try to auto-select
        //if (string.IsNullOrEmpty(_settings.TargetWindowTitle) && _settings.AllowOnlyRuleDefinedWindows)
        //{
        //    var visibleWindows = _windowMonitor.GetVisibleWindows();
        //    var rulesData = _windowMatchingService.GetRulesData();
        //    bool found = false;

        //    if (rulesData != null)
        //    {
        //        foreach (var ruleData in rulesData)
        //        {
        //            if (ruleData?.TitlePrefixes == null)
        //                continue;

        //            foreach (var prefix in ruleData.TitlePrefixes)
        //            {
        //                if (string.IsNullOrEmpty(prefix))
        //                    continue;

        //                var match = visibleWindows.FirstOrDefault(w =>
        //                    string.Equals(w.ClassName, ruleData.ClassName, StringComparison.Ordinal) &&
        //                    w.Title != null && w.Title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        //                if (match != null)
        //                {
        //                    // Set as selected target
        //                    _selectedWindow = match;

        //                    // Persist the partial title (prefix) in settings rather than full title
        //                    _settings.TargetWindowTitle = prefix;
        //                    _settings.TargetWindowClassName = match.ClassName;

        //                    // Try to select the corresponding item in the combo box if present
        //                    foreach (WindowInfo item in WindowComboBox.Items)
        //                    {
        //                        if (item.Handle == match.Handle)
        //                        {
        //                            WindowComboBox.SelectedItem = item;
        //                            break;
        //                        }
        //                    }

        //                    // Persist the auto-selection
        //                    _settingsManager.SaveSettings(_settings);

        //                    found = true;
        //                    break;
        //                }
        //            }

        //            if (found)
        //                break;
        //        }
        //    }

            if (!found)
            {
                // Indicate to the user that no rule-matching windows are available
                PlaceholderText.Text = "No available target windows matching rules";
            }
            //else
            //{
            //    // Ensure refocus button reflects new selection
            //    UpdateRefocusingButton();
            //}
        //}

        // Try to restore the previously selected window
        if (!string.IsNullOrEmpty(_settings.TargetWindowTitle))
        {
            RestoreTargetWindow();
        }

        // If Hide Mode is enabled and we have a selected window, start the service if window exists
        if (_settings.IsHideMode && _selectedWindow != null)
        {
            // If window exists and service isn't running, start it
            if (_selectedWindow.Handle != IntPtr.Zero && _selectedWindow.Exists)
            {
                _focusPullerService.Start(_selectedWindow.Handle, (int)DelaySlider.Value, _selectedWindow.ClassName, _selectedWindow.Title);
            }
            else
            {
                // If handle is zero (placeholder) but hide mode is enabled, start service with class/title so it can watch for appearance
                _focusPullerService.Start(_selectedWindow.Handle, (int)DelaySlider.Value, _selectedWindow.ClassName, _selectedWindow.Title);
            }
            // Otherwise we're in waiting mode (refocusing is on, waiting for window to appear)
        }

        // Refresh the window list to apply AllowOnlyRuleDefinedWindows setting
        RefreshWindowList();

        //UpdateRefocusingButton();
    }

    private void SaveSettings()
    {
        _settings.RefocusDelayMs = (int)DelaySlider.Value;
        _settings.IsHideMode = HideModeCheckBox.IsChecked ?? false;

        // Save matching rules from the service
        _settings.MatchingRules = _windowMatchingService.GetRulesData();

        if (_selectedWindow != null)
        {
            // If the selected window matches a rule, save the rule prefix instead of the full title
            var rule = _windowMatchingService.FindMatchingRule(_selectedWindow.ClassName, _selectedWindow.Title);
            if (rule != null)
            {
                // Find the first prefix that matches the window title
                var matchingPrefix = rule.TitlePrefixes.FirstOrDefault(p =>
                    !string.IsNullOrEmpty(p) && _selectedWindow.Title != null && _selectedWindow.Title.StartsWith(p, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(matchingPrefix))
                {
                    _settings.TargetWindowTitle = matchingPrefix;
                }
                else
                {
                    // Fallback to full title if no specific prefix matched for some reason
                    _settings.TargetWindowTitle = _selectedWindow.Title;
                }

                _settings.TargetWindowClassName = _selectedWindow.ClassName;
            }
            else
            {
                _settings.TargetWindowTitle = _selectedWindow.Title;
                _settings.TargetWindowClassName = _selectedWindow.ClassName;
            }
        }

        _settingsManager.SaveSettings(_settings);
    }

    private void RestoreTargetWindow()
    {
        var windows = _windowMonitor.GetVisibleWindows();
        
        // Try to find exact match first, then use matching rules
        var targetWindow = windows.FirstOrDefault(w =>
                _windowMatchingService.WindowsMatch(
                    _settings.TargetWindowClassName,
                    _settings.TargetWindowTitle,
                    w.ClassName,
                    w.Title
                ));

        if (targetWindow != null)
        {
            // Window exists - add it normally
            _selectedWindow = targetWindow;
            
            // Update the saved title if it matches a rule (to keep current full title)
            if (_windowMatchingService.ShouldUpdateTitle(
                _settings.TargetWindowClassName,
                _settings.TargetWindowTitle,
                targetWindow.ClassName,
                targetWindow.Title))
            {
                _settings.TargetWindowTitle = targetWindow.Title;
            }
            
            // Select it in the combo box if it exists
            foreach (WindowInfo item in WindowComboBox.Items)
            {
                if (item.Handle == targetWindow.Handle)
                {
                    WindowComboBox.SelectedItem = item;
                    break;
                }
            }
            
            // Note: Don't call StartRefocusing here - let LoadSettings handle it

            UpdateRefocusingButton();
        }
        else if (!string.IsNullOrEmpty(_settings.TargetWindowTitle))
        {
            // Window doesn't exist - create a placeholder entry and mark it as non-existent
            var placeholderWindow = new WindowInfo(
                IntPtr.Zero,
                _settings.TargetWindowTitle,
                _settings.TargetWindowClassName)
            {
                Exists = false
            };
            
            _selectedWindow = placeholderWindow;
            WindowComboBox.Items.Add(placeholderWindow);
            WindowComboBox.SelectedItem = placeholderWindow;
            
            // Note: Don't call StartRefocusing here - let LoadSettings handle it

            UpdateRefocusingButton();
        }
    }

    private void RefreshWindowList()
    {
        var windows = _windowMonitor.GetVisibleWindows();
        var previousSelection = WindowComboBox.SelectedItem as WindowInfo;

        // Check if there's a placeholder window (non-existent window from settings) that should be preserved
        WindowInfo placeholderWindow = null;
        foreach (WindowInfo item in WindowComboBox.Items)
        {
            if (item.Handle == IntPtr.Zero && !item.Exists)
            {
                placeholderWindow = item;
                break;
            }
        }

        WindowComboBox.Items.Clear();

        // Add the placeholder window first if it exists
        if (placeholderWindow != null)
        {
            WindowComboBox.Items.Add(placeholderWindow);
        }

        // If settings require only rule-defined windows, filter the list
        if (_settings != null && _settings.AllowOnlyRuleDefinedWindows)
        {
            windows = windows.Where(w => _windowMatchingService.FindMatchingRule(w.ClassName, w.Title) != null).ToList();
        }

        // Add all visible windows (or filtered set). Also, if no target is set and a matching window appears, auto-select it.
        foreach (var window in windows)
        {
            WindowComboBox.Items.Add(window);

            if (_selectedWindow == null && !string.IsNullOrEmpty(_settings?.TargetWindowClassName) && string.IsNullOrEmpty(_settings?.TargetWindowTitle))
            {
                // No saved title but class is set? skip
            }

            // If no target is selected at all, and rule-filtering is enabled, select the first window that matches a rule
            if (_selectedWindow == null && _settings != null && _settings.AllowOnlyRuleDefinedWindows)
            {
                var rule = _windowMatchingService.FindMatchingRule(window.ClassName, window.Title);
                if (rule != null)
                {
                    WindowComboBox.SelectedItem = window;
                    _selectedWindow = window;

                    // Save selection as prefix if rule matched
                    var matchingPrefix = rule.TitlePrefixes.FirstOrDefault(p => !string.IsNullOrEmpty(p) && window.Title != null && window.Title.StartsWith(p, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(matchingPrefix))
                    {
                        _settings.TargetWindowTitle = matchingPrefix;
                        _settings.TargetWindowClassName = window.ClassName;
                        _settingsManager.SaveSettings(_settings);
                    }

                    // If refocusing is enabled (hide mode), start the focus service immediately for this window
                    if (_isRefocusing && _selectedWindow.Handle != IntPtr.Zero)
                    {
                        _focusPullerService.Start(_selectedWindow.Handle, (int)DelaySlider.Value, _selectedWindow.ClassName, _selectedWindow.Title);
                    }

                    break;
                }
            }
        }

        // Try to restore selection
        if (previousSelection != null)
        {
            foreach (WindowInfo item in WindowComboBox.Items)
            {
                if (item.Handle == previousSelection.Handle || 
                    (item.Handle == IntPtr.Zero && 
                     item.Title == previousSelection.Title && 
                     item.ClassName == previousSelection.ClassName))
                {
                    WindowComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        if (WindowComboBox.SelectedItem == null && WindowComboBox.Items.Count > 0)
        {
            PlaceholderText.Text = "Click to select target window";
        }

        UpdateRefocusingButton();
    }

    private void StartRefocusing()
    {
        // Only show error if no window is selected at all (not even a placeholder)
        if (_selectedWindow == null)
        {
            MessageBox.Show("Please select a target window first.", "No Target Window",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            _isRefocusing = false;
            UpdateRefocusingButton();
            return;
        }

        // If window doesn't exist yet (Handle == IntPtr.Zero), that's okay - we'll wait for it
        // Only start the focus puller service if the window actually exists
        if (_selectedWindow.Handle != IntPtr.Zero)
        {
            _focusPullerService.Start(_selectedWindow.Handle, (int)DelaySlider.Value);
        }
        
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
        // Additionally allow enabling when Hide Mode/refocusing is active so the user can turn it off even if the window doesn't currently exist.
        RefocusingButton.IsEnabled = _selectedWindow != null && (_selectedWindow.Exists || _isRefocusing);
    }

    private void WindowComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var previousWindow = _selectedWindow;
        _selectedWindow = WindowComboBox.SelectedItem as WindowInfo;

        // If window was changed while refocusing is on, disable refocusing
        if (previousWindow != null && _selectedWindow != null && _isRefocusing)
        {
            // Check if it's actually a different window (not just the same window being re-selected)
            bool isDifferentWindow = previousWindow.ClassName != _selectedWindow.ClassName || 
                                    previousWindow.Title != _selectedWindow.Title;
            
            if (isDifferentWindow)
            {
                StopRefocusing();
                return; // StopRefocusing already calls SaveSettings and UpdateRefocusingButton
            }
        }

        if (_selectedWindow != null && _isRefocusing)
        {
            // If service is already running, restart it with new target info
            if (_focusPullerService.IsRunning)
            {
                _focusPullerService.Stop();
            }

            _focusPullerService.Start(_selectedWindow.Handle, (int)DelaySlider.Value, _selectedWindow.ClassName, _selectedWindow.Title);
            SaveSettings();
        }
        
        // Update window status immediately when selection changes
        UpdateWindowStatus();

        UpdateRefocusingButton();
    }

    private void WindowCheckTimer_Tick(object sender, EventArgs e)
    {
        UpdateWindowStatus();
    }

    private void UpdateWindowStatus()
    {
        // If no window is selected, clear the label
        if (_selectedWindow == null)
        {
            WindowStatusLabel.Text = "";
            UpdateRefocusingButton();
            return;
        }

        // Get all currently visible windows
        var currentWindows = _windowMonitor.GetVisibleWindows();
        
        // Check if the selected window still exists (exact match first, then rule-based)
        var matchingWindow = currentWindows.FirstOrDefault(w =>
            w.Title == _selectedWindow.Title &&
            w.ClassName == _selectedWindow.ClassName);

        // If no exact match, try using matching rules
        if (matchingWindow == null)
        {
            matchingWindow = currentWindows.FirstOrDefault(w =>
                _windowMatchingService.WindowsMatch(
                    _selectedWindow.ClassName,
                    _selectedWindow.Title,
                    w.ClassName,
                    w.Title
                ));
        }

        if (matchingWindow == null)
        {
            // Window no longer exists
            WindowStatusLabel.Text = "Window not open / available";
            _selectedWindow.Exists = false;
            
            // If refocusing is on, stop the service (but keep _isRefocusing = true)
            if (_isRefocusing)
            {
                _focusPullerService.Stop();
            }
        }
        else
        {
            // Window exists
            WindowStatusLabel.Text = "";
            
            // Update title if it matches a rule and has changed
            if (_windowMatchingService.ShouldUpdateTitle(
                _selectedWindow.ClassName,
                _selectedWindow.Title,
                matchingWindow.ClassName,
                matchingWindow.Title))
            {
                // Update the title in the selected window and combo box
                _selectedWindow.Title = matchingWindow.Title;
                
                // Find the item in the combo box and update it
                foreach (WindowInfo item in WindowComboBox.Items)
                {
                    if (item.Handle == _selectedWindow.Handle ||
                        (item.Handle == IntPtr.Zero &&
                         item.ClassName == _selectedWindow.ClassName))
                    {
                        item.Title = matchingWindow.Title;
                        break;
                    }
                }
                
                // Save the updated title
                SaveSettings();
            }
            
            // If this was a placeholder window (Handle == IntPtr.Zero), update it with the real handle
            if (_selectedWindow.Handle == IntPtr.Zero)
            {
                _selectedWindow.Handle = matchingWindow.Handle;
                
                // If refocusing is enabled and we just got a valid handle, start the service
                if (_isRefocusing)
                {
                    _focusPullerService.Start(_selectedWindow.Handle, (int)DelaySlider.Value, _selectedWindow.ClassName, _selectedWindow.Title);
                }
            }
            
            // Update the Exists property
            _selectedWindow.Exists = true;
        }
        
        // Update Exists property for all items in the combo box
        foreach (WindowInfo item in WindowComboBox.Items)
        {
            if (item.Handle != IntPtr.Zero)
            {
                // Check if this window still exists (exact or rule-based match)
                var exists = currentWindows.Any(w => w.Handle == item.Handle) ||
                             currentWindows.Any(w => _windowMatchingService.WindowsMatch(
                                 item.ClassName, item.Title, w.ClassName, w.Title));
                item.Exists = exists;
            }
            else
            {
                // This is a placeholder - check if a matching window now exists
                var exists = currentWindows.Any(w =>
                    w.Title == item.Title &&
                    w.ClassName == item.ClassName) ||
                    currentWindows.Any(w => _windowMatchingService.WindowsMatch(
                        item.ClassName, item.Title, w.ClassName, w.Title));
                item.Exists = exists;
                
                // If the window now exists, update the handle and title
                if (exists)
                {
                    var existingWindow = currentWindows.FirstOrDefault(w =>
                        w.Title == item.Title &&
                        w.ClassName == item.ClassName);
                    
                    if (existingWindow == null)
                    {
                        existingWindow = currentWindows.First(w =>
                            _windowMatchingService.WindowsMatch(
                                item.ClassName, item.Title, w.ClassName, w.Title));
                    }
                    
                    item.Handle = existingWindow.Handle;
                    
                    // Update title if using a matching rule
                    if (_windowMatchingService.ShouldUpdateTitle(
                        item.ClassName, item.Title,
                        existingWindow.ClassName, existingWindow.Title))
                    {
                        item.Title = existingWindow.Title;
                    }
                }
            }
        }

        UpdateRefocusingButton();
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
        if (HideModeCheckBox.IsChecked == true && _selectedWindow != null && !_isRefocusing)
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
            // Don't change _isRefocusing state - let it stay as user set it
            // The service will restart automatically when window reappears (via UpdateWindowStatus)
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
        // Do not call Application.Current.Shutdown() here because Closing is triggered by application shutdown already
    }
}