using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusPuller;

/// <summary>
/// Defines a rule for matching windows with flexible title patterns
/// </summary>
public class WindowFinderRule
{
    public string ClassName { get; set; }
    public string[] TitlePrefixes { get; set; }

    public WindowFinderRule(string className, params string[] titlePrefixes)
    {
        ClassName = className;
        TitlePrefixes = titlePrefixes;
    }

    /// <summary>
    /// Checks if this rule matches the given window
    /// </summary>
    public bool Matches(string className, string title)
    {
        if (className != ClassName)
        {
            return false;
        }

        foreach (var prefix in TitlePrefixes)
        {
            if (title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
