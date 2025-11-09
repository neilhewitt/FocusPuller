using FocusPuller.Models;
using System.Text.Json;
using System.IO;
using System.Linq;

namespace FocusPuller.Services;

/// <summary>
/// Manages window matching rules for flexible title pattern matching
/// </summary>
public class WindowMatchingService
{
    private readonly List<WindowMatchingRule> _rules;

    public WindowMatchingService()
    {
        _rules = new List<WindowMatchingRule>();
    }

    /// <summary>
    /// Initializes rules from settings, or uses defaults if settings are empty
    /// </summary>
    public void LoadRules(List<WindowMatchingRuleData> savedRules)
    {
        _rules.Clear();

        if (savedRules != null && savedRules.Count > 0)
        {
            // Load rules from settings
            foreach (var ruleData in savedRules)
            {
                _rules.Add(WindowMatchingRule.FromData(ruleData));
            }
        }
        else
        {
            // Use default rules if none are saved
            InitializeDefaultRules();
        }
    }

    /// <summary>
    /// Gets the current rules as serializable data for saving to settings
    /// </summary>
    public List<WindowMatchingRuleData> GetRulesData()
    {
        return _rules.Select(r => r.ToData()).ToList();
    }

    private void InitializeDefaultRules()
    {
        try
        {
            var appBase = AppContext.BaseDirectory;
            var defaultRulesPath = Path.Combine(appBase, "defaultrules.json");
            if (File.Exists(defaultRulesPath))
            {
                var json = File.ReadAllText(defaultRulesPath);
                var rulesData = JsonSerializer.Deserialize<List<WindowMatchingRuleData>>(json);
                if (rulesData != null)
                {
                    foreach (var rd in rulesData)
                    {
                        _rules.Add(WindowMatchingRule.FromData(rd));
                    }
                    return;
                }
            }
        }
        catch
        {
            // ignore and fall back to hardcoded defaults
        }

        // Hardcoded fallback
        _rules.Add(new WindowMatchingRule(
            "AceApp",
            "Microsoft Flight Simulator 2024 -",
            "Microsoft Flight Simulator 2020 -",
            "Microsoft Flight Simulator -"
        ));
    }

    /// <summary>
    /// Checks if two windows match according to any rule, or by exact match
    /// </summary>
    public bool WindowsMatch(string className1, string title1, string className2, string title2)
    {
        // First check for exact match
        if (className1 == className2 && title1 == title2)
            return true;

        // Then check if any rule matches
        foreach (var rule in _rules)
        {
            if (rule.MatchesSavedWindow(className1, title1, title2) && className2 == className1)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Finds a matching rule for the given window
    /// </summary>
    public WindowMatchingRule FindMatchingRule(string className, string title)
    {
        foreach (var rule in _rules)
        {
            if (rule.Matches(className, title))
                return rule;
        }

        return null;
    }

    /// <summary>
    /// Determines if a window title should be updated based on matching rules
    /// </summary>
    public bool ShouldUpdateTitle(string savedClassName, string savedTitle, string currentClassName, string currentTitle)
    {
        var rule = FindMatchingRule(currentClassName, currentTitle);
        if (rule == null)
            return false;

        // Check if the saved window also matches this rule
        return rule.MatchesSavedWindow(savedClassName, savedTitle, currentTitle);
    }
}
