using System.IO;
using System.Text.Json;

namespace FocusPuller;

public class SettingsValues
{
    public int RefocusDelayInMilliseconds { get; set; } = 5000;
    public bool IsHideMode { get; set; } = false;
    public string TargetWindowTitle { get; set; } = "";
    public string TargetWindowClassName { get; set; } = "";
    public bool AllowOnlyRuleDefinedWindows { get; set; } = false;
    public List<WindowFinderRule> MatchingRules { get; set; } = new List<WindowFinderRule>();
}

public class Settings
{
    public static string GetDefaultSettingsPath()
    {
        var programDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "FocusPuller");

        Directory.CreateDirectory(programDataPath);
        return Path.Combine(programDataPath, "settings.json");
    }

    private string _path;

    public SettingsValues Values { get; set; } = new SettingsValues();

    public Settings(string? path = null)
    {
        _path = path ?? GetDefaultSettingsPath();
        Load();
    }

    public void Save()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(Values, options);
            File.WriteAllText(_path, json);
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
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                Values = JsonSerializer.Deserialize<SettingsValues>(json);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
        }

        try
        {
            // Look for defaultrules.json next to the application executable
            var appBase = AppContext.BaseDirectory;
            var defaultRulesPath = Path.Combine(appBase, "defaultrules.json");
            if (File.Exists(defaultRulesPath))
            {
                var rulesJson = File.ReadAllText(defaultRulesPath);
                var rulesData = JsonSerializer.Deserialize<List<WindowFinderRule>>(rulesJson);
                if (rulesData != null && rulesData.Count > 0)
                {
                    Values.MatchingRules = rulesData;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load default rules: {ex.Message}");
        }
    }
}
