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

        // No settings file exists - initialize default settings
        var defaultSettings = new AppSettings();

        try
        {
            // Look for defaultrules.json next to the application executable
            var appBase = AppContext.BaseDirectory;
            var defaultRulesPath = Path.Combine(appBase, "defaultrules.json");
            if (File.Exists(defaultRulesPath))
            {
                var rulesJson = File.ReadAllText(defaultRulesPath);
                var rulesData = JsonSerializer.Deserialize<List<WindowMatchingRuleData>>(rulesJson);
                if (rulesData != null && rulesData.Count > 0)
                {
                    defaultSettings.MatchingRules = rulesData;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load default rules: {ex.Message}");
        }

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
