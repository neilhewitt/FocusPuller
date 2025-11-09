namespace FocusPuller.Models;

public class AppSettings
{
    public int RefocusDelayMs { get; set; } = 5000;
    public bool IsHideMode { get; set; } = false;
    public string TargetWindowTitle { get; set; } = "";
    public string TargetWindowClassName { get; set; } = "";
    public bool AllowOnlyRuleDefinedWindows { get; set; } = true;
    public List<WindowMatchingRuleData> MatchingRules { get; set; } = new List<WindowMatchingRuleData>();
}

/// <summary>
/// Serializable representation of a WindowMatchingRule
/// </summary>
public class WindowMatchingRuleData
{
    public string ClassName { get; set; }
    public List<string> TitlePrefixes { get; set; } = new List<string>();
}
