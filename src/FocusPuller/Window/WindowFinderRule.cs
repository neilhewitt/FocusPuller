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

    /// <summary>
    /// Checks if a saved window title matches a current window title using this rule
    /// </summary>
    public bool MatchesWindow(string className, string existingTitle, string currentTitle)
    {
        if (className != ClassName)
        {
            return false;
        }

        // Check if both titles match any of the prefixes
        bool existingMatches = false;
        bool currentMatches = false;

        foreach (var prefix in TitlePrefixes)
        {
            if (!string.IsNullOrEmpty(existingTitle) && existingTitle.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                existingMatches = true;
            }

            if (!string.IsNullOrEmpty(currentTitle) && currentTitle.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                currentMatches = true;
            }
        }

        return existingMatches && currentMatches;
    }
}
