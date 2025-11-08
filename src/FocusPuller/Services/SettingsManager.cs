using System.IO;
using System.Text.Json;
using FocusPuller.Models;

namespace FocusPuller.Services;

public class SettingsManager
{
    private readonly string _settingsPath;

    public SettingsManager()
    {
        var programDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "FocusPuller");
        
        Directory.CreateDirectory(programDataPath);
        _settingsPath = Path.Combine(programDataPath, "settings.json");
    }

    public AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
        }

        // No settings file exists, create default settings and save them
        var defaultSettings = new AppSettings();
        SaveSettings(defaultSettings);
        return defaultSettings;
    }

    public void SaveSettings(AppSettings settings)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }
}
