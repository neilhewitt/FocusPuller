# FocusPuller

A Windows WPF application that automatically returns focus to a specified window after a defined period of inactivity.

## Features

- **Auto-Refocus**: Automatically brings a target window back to focus after keyboard/mouse/touch inactivity
- **Configurable Delay**: Set the refocus delay from 1 to 30 seconds
- **Window Selection**: Choose from a list of all visible windows by title and class name
- **System Tray Integration**: Minimizes to system tray instead of taskbar
- **Hide Mode**: Start minimized to tray with refocusing automatically enabled
- **Persistent Settings**: All settings are saved and restored between sessions

## How to Use

1. **Launch the application** - The UI will appear showing all configuration options

2. **Select Target Window**:
   - Click "Refresh Window List" to populate available windows
   - Choose the window you want to keep in focus from the dropdown

3. **Set Refocus Delay**:
   - Use the slider to adjust the delay (1000-30000 milliseconds)
   - This is how long the app waits after the last user input before refocusing

4. **Enable Refocusing**:
   - Check "Enable Refocusing" to start the auto-focus behavior

5. **Configure Hide Mode** (optional):
   - Check "Hide Mode" if you want the app to start minimized with refocusing enabled on next launch
   - This is useful for automatic startup scenarios

6. **Minimize to Tray**:
   - Click "Minimize to System Tray" or close the window
   - Right-click the system tray icon to restore or exit

## Settings

Settings are automatically saved to:
```
%APPDATA%\FocusPuller\settings.json
```

Includes:
- Refocus delay
- Target window (title and class name)
- Refocusing enabled/disabled state
- Hide mode preference

## Technical Details

- **Framework**: .NET 10 (Windows)
- **UI**: WPF
- **Dependencies**: Hardcodet.NotifyIcon.Wpf (for system tray support)

## Building

```bash
cd src/FocusPuller
dotnet restore
dotnet build
```

## Running

```bash
dotnet run --project src/FocusPuller/FocusPuller.csproj
```

Or run the compiled executable from:
```
src/FocusPuller/bin/Debug/net10.0-windows/FocusPuller.exe
```

## Notes

- The target window must remain open for refocusing to work
- If the target window is closed, refocusing is automatically disabled
- The app monitors both focus changes and user input activity
- Refocusing only occurs when BOTH conditions are met:
  1. The configured delay time has passed since focus was lost
  2. There has been no user input for the configured delay time
