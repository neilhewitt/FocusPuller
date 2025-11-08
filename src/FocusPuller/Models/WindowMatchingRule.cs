namespace FocusPuller.Models;

/// <summary>
/// Defines a rule for matching windows with flexible title patterns
/// </summary>
public class WindowMatchingRule
{
    public string ClassName { get; set; }
    public string[] TitlePrefixes { get; set; }

    public WindowMatchingRule(string className, params string[] titlePrefixes)
    {
        ClassName = className;
        TitlePrefixes = titlePrefixes;
    }

    /// <summary>
    /// Creates a WindowMatchingRule from serializable data
    /// </summary>
    public static WindowMatchingRule FromData(WindowMatchingRuleData data)
    {
        return new WindowMatchingRule(data.ClassName, data.TitlePrefixes.ToArray());
    }

    /// <summary>
    /// Converts this rule to serializable data
    /// </summary>
    public WindowMatchingRuleData ToData()
    {
        return new WindowMatchingRuleData
        {
            ClassName = ClassName,
            TitlePrefixes = new List<string>(TitlePrefixes)
        };
    }

    /// <summary>
    /// Checks if this rule matches the given window
    /// </summary>
    public bool Matches(string className, string title)
    {
        if (className != ClassName)
            return false;

        foreach (var prefix in TitlePrefixes)
        {
            if (title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a saved window title matches a current window title using this rule
    /// </summary>
    public bool MatchesSavedWindow(string savedClassName, string savedTitle, string currentTitle)
    {
        if (savedClassName != ClassName)
            return false;

        // Check if both titles match any of the prefixes
        bool savedMatches = false;
        bool currentMatches = false;

        foreach (var prefix in TitlePrefixes)
        {
            if (savedTitle.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                savedMatches = true;
            if (currentTitle.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                currentMatches = true;
        }

        return savedMatches && currentMatches;
    }
}
