# FocusPuller

A Windows WPF application that automatically returns focus to a specified window after a defined period of inactivity.

## Features

- **Auto-Refocus**: Automatically brings a target window back to focus after keyboard/mouse/touch inactivity
- **Configurable Delay**: Set the refocus delay from 100ms to 30000ms (0.1 - 30 seconds)
- **Auto-Targeting**: Picks the target window based on rules (title/class name) which are configurable
- **System Tray Integration**: Minimizes to system tray instead of taskbar
- **Hide Mode**: Start minimized to tray with refocusing automatically enabled
- **Persistent Settings**: All settings are saved and restored between sessions

This app was built to help me keep Microsoft Flight Simulator focussed while using other apps alongside it, since keyboard commands and controllers don't work if the focus is lost. 
The default rule set is configured to target Flight Simulator, but you can change it to any window type you like.

## Window targeting rules

The default rules are stored in
```
%APPDATA%\FocusPuller\defaultrules.json
```

If you want to customize the rules, edit the file before first run. After first run, the rules are saved to the `settings.json` file and you can change them again there if you need to.

The rules file is a JSON array of objects with the following properties:

- **ClassName**: The class name of the window to match - case sensitive, exact match
- One or more **TitlePrefixes**: An array of title prefixes to match (for example, one for MSFS 2024 and one for MSFS 2020)

Title matching is case sensitive and partial - the actual window title only needs to *start* with one of the prefixes. This allows for dynamic titles that may include version numbers or other changing information, which is the case for Microsoft Flight Simulator.

## How to Use

1. **Launch the application**
   - The app will tell you that no target window is available if you haven't launched the target app yet

2. **Launch the target application/window** that you want to keep focused (by default Microsoft Flight Simulator 2024/2020) 
   - FocusPuller should notice and start tracking it

3. **Set Refocus Delay**
   - Use the slider to adjust the delay (1000-30000 milliseconds) or type directly into the box + ENTER
   - This is how long the app waits after the last user input before refocusing

4. **Enable Refocusing**
   - Click the refocusing button to start auto-refocus

5. **Configure Hide Mode** (optional)
   - Check "Hide Mode" if you want the app to start minimized with refocusing enabled on next launch
   - This is useful for automatic startup scenarios

6. **Minimize to Tray**
   - The app minimises to the system tray instead of the taskbar
   - Right-click the system tray icon to restore or exit

## Settings

Settings are automatically saved as they change to:
```
%APPDATA%\FocusPuller\settings.json
```
- Refocus delay
- Target window (title and class name)
- Refocusing enabled/disabled state
- Hide mode preference
- Target window selection rules

## Details

- **Framework**: .NET 9 (Windows)
- **UI**: WPF
- **Dependencies**: Hardcodet.NotifyIcon.Wpf (for system tray support)

## Building

You can open the project in Visual Studio or build from the command line:

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
src/FocusPuller/bin/Debug/net9.0-windows/FocusPuller.exe
```

## Known Issues

Because Windows has anti-focus-stealing measures, refocusing may not work in some scenarios, especially if the user has recently interacted with other applications. You may see the target app icon flash in the taskbar when refocusing is attempted but blocked.

The app uses a non-standard approach to bringing the target window to the foreground, which involves temporarily setting the target window to always-on-top and then simulating a mouse click on it to give it the focus. The window is then set back to normal z-ordering if it wasn't already always-on-top. On rare occasions, the app may remain stuck in always-on-top mode and will need to be terminated and restarted to fix this.

This approach is tested to work with Microsoft Flight Simulator 2024/2020 on Windows 11 25H2, but may not work with all applications, and may be affected by future simulator updates or Windows system updates.

## Notes

- The target window must remain open for refocusing to work
- If the target window is closed, refocusing is automatically disabled
- If the target window becomes available again, refocusing is re-enabled automatically **in hide mode only**
- Refocusing only happens once the configured delay time has passed since focus was lost **and** there has been no user input for the configured delay time, which might exceed the configured delay time in total
