namespace FocusPuller;

public class SettingsValues
{
    private bool _hotkeyUseControl = true;
    private bool _hotkeyUseAlt = true;
    private bool _hotkeyUseShift = true;
    private ushort _hotkeyVirtualKeyCode = (ushort)VirtualKey.D0;

    public int RefocusDelayInMilliseconds { get; set; } = 5000;
    public bool IsHideMode { get; set; } = false;
    public string TargetWindowTitle { get; set; } = "";
    public string TargetWindowClassName { get; set; } = "";
    public bool AllowOnlyRuleDefinedWindows { get; set; } = false;
    public List<WindowFinderRule> MatchingRules { get; set; } = new List<WindowFinderRule>();
    
    public string HotKeyCombination
    {
        get
        {
            var keyCombinationParts = new List<string>();
            if (_hotkeyUseControl) keyCombinationParts.Add("CTRL");
            if (_hotkeyUseAlt) keyCombinationParts.Add("ALT");
            if (_hotkeyUseShift) keyCombinationParts.Add("SHIFT");

            string keyName = Enum.GetName((VirtualKey)_hotkeyVirtualKeyCode);
            if (keyName.Length == 2 && keyName[0] == 'D')
            {
                keyName = keyName[1..];
            }
            keyCombinationParts.Add(keyName);

            return string.Join("+", keyCombinationParts);
        }

        set
        {
            var keyCombinationParts = value.ToUpper().Split('+');
            _hotkeyUseControl = keyCombinationParts.Contains("CTRL");
            _hotkeyUseAlt = keyCombinationParts.Contains("ALT");
            _hotkeyUseShift = keyCombinationParts.Contains("SHIFT");

            // find _hotkeyVirtualKeyCode from the remaining part
            // if the key part is a single digit, convert to D0-D9
            string keyPart = keyCombinationParts.FirstOrDefault(p => p != "CTRL" && p != "ALT" && p != "SHIFT");
            if (keyPart != null)
            {
                if (keyPart.Length == 1 && char.IsDigit(keyPart[0]))
                {
                    keyPart = "D" + keyPart;
                }
                if (Enum.TryParse<VirtualKey>(keyPart, out var vk))
                {
                    _hotkeyVirtualKeyCode = (ushort)vk;
                }
                else
                {
                    _hotkeyVirtualKeyCode = 0; // invalid key
                }
            }
            else
            {
                _hotkeyVirtualKeyCode = 0; // no key part found
            }
        }
    }

    public (bool useControl, bool useAlt, bool useShift, uint combinedModifiers, ushort virtualKeyCode, int keyCount) GetHotkeyInfo()
    {
        int keyCount = 1; // there's always the main key
        if (_hotkeyUseControl) keyCount++;
        if (_hotkeyUseAlt) keyCount++;
        if (_hotkeyUseShift) keyCount++;

        return (_hotkeyUseControl, _hotkeyUseAlt, _hotkeyUseShift, GetHotkeyModifiers(), _hotkeyVirtualKeyCode, keyCount);
    }
    
    public bool HasValidHotkey()
    {
        // At least one modifier must be selected and a valid key code must be set
        return (_hotkeyUseControl || _hotkeyUseAlt || _hotkeyUseShift) && _hotkeyVirtualKeyCode > 0;
    }

    private uint GetHotkeyModifiers()
    {
        uint modifiers = 0;

        if (_hotkeyUseControl)
            modifiers |= NativeMethods.MOD_CONTROL;

        if (_hotkeyUseAlt)
            modifiers |= NativeMethods.MOD_ALT;

        if (_hotkeyUseShift)
            modifiers |= NativeMethods.MOD_SHIFT;

        return modifiers;
    }
}
