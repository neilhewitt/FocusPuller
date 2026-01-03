using System.IO;
using System.Text.Json;

namespace FocusPuller;

public class Settings
{
    public const string SETTINGS_FILENAME = "settings.json";
    public const string RULES_FILENAME = "defaultrules.json";

    public static string GetDefaultSettingsFolder()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "FocusPuller");
    }

    private string _settingsFolder;
    private string _settingsPath;

    public SettingsValues Values { get; set; } = new SettingsValues();

    public Settings()
    {
        _settingsFolder = GetDefaultSettingsFolder();
        
        // ensure the ProgramData/FocusPuller folder exists
        Directory.CreateDirectory(_settingsFolder);

        _settingsPath = Path.Combine(_settingsFolder, SETTINGS_FILENAME);
        Load();
        ValidateSettings();
    }

    public void Save()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var json = JsonSerializer.Serialize(Values, options);
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                Values = JsonSerializer.Deserialize<SettingsValues>(json);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
        }

        try
        {
            if (Values.WindowFinderRules?.Count == 0)
            {
                // Look for defaultrules.json
                var appRulesPath = Path.Combine(AppContext.BaseDirectory, RULES_FILENAME);
                if (File.Exists(appRulesPath))
                {
                    var rulesJson = File.ReadAllText(appRulesPath);
                    var rulesData = JsonSerializer.Deserialize<List<WindowFinderRule>>(rulesJson);
                    if (rulesData != null && rulesData.Count > 0)
                    {
                        Values.WindowFinderRules = rulesData;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load default rules: {ex.Message}");
        }
    }

    private void ValidateSettings()
    {
        // Ensure hotkey configuration is valid - reset to defaults if not
        if (!Values.HasValidHotkey())
        {
            System.Diagnostics.Debug.WriteLine("Invalid hotkey configuration detected. Resetting to defaults.");
            Values.HotKeyCombination = "CTRL+ALT+SHIFT+0";
            Save();
        }
    }
}
