using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace FocusPuller;

public class WindowFinder
{
    public List<WindowFinderRule> Rules { get; init; }

    public WindowFinder(Settings settings)
    {
        Rules = settings.Values.MatchingRules ?? InitialiseDefaultRules();
    }

    public WindowInfo FindTargetWindow()
    {
        var windows = GetVisibleWindows();

        // go through the rules in order, and find the first matching window
        foreach (var rule in Rules)
        {
            foreach (var window in windows)
            {
                if (rule.Matches(window.ClassName, window.Title))
                {
                    return window;
                }
            }
        }

        return null;
    }

    public WindowFinderRule FindRule(string className, string title)
    {
        foreach (var rule in Rules)
        {
            if (rule.Matches(className, title))
            {
                return rule;
            }
        }

        return null;
    }


    public bool IsVisible(WindowInfo windowInfo)
    {
        if (windowInfo == null)
        {
            return false;
        }

        var windows = GetVisibleWindows();
        return windows.Any(w => w.ClassName == windowInfo.ClassName && w.Title == windowInfo.Title);
    }

    private List<WindowInfo> GetVisibleWindows()
    {
        var windows = new List<WindowInfo>();

        NativeMethods.EnumWindows((hWnd, lParam) =>
        {
            if (NativeMethods.IsWindowVisible(hWnd))
            {
                var titleBuilder = new StringBuilder(256);
                var classBuilder = new StringBuilder(256);

                NativeMethods.GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                NativeMethods.GetClassName(hWnd, classBuilder, classBuilder.Capacity);

                var title = titleBuilder.ToString();
                var className = classBuilder.ToString();

                // Only include windows with titles
                if (!string.IsNullOrWhiteSpace(title))
                {
                    windows.Add(new WindowInfo(hWnd, title, className));
                }
            }

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private List<WindowFinderRule> InitialiseDefaultRules()
    {
        try
        {
            var appBase = AppContext.BaseDirectory;
            var defaultRulesPath = Path.Combine(appBase, "defaultrules.json");
            if (File.Exists(defaultRulesPath))
            {
                var json = File.ReadAllText(defaultRulesPath);
                var rulesData = JsonSerializer.Deserialize<List<WindowFinderRule>>(json);
                return rulesData;
            }
        }
        catch
        {
            // ignore - no rules exist, user can add some in the JSON
        }

        return new List<WindowFinderRule>(); // empty
    }
}
