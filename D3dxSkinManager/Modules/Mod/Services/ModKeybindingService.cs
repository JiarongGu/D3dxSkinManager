using System.Text.RegularExpressions;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Context.Services;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Service for parsing and managing mod keybindings from .ini files
/// </summary>
public interface IModKeybindingService
{
    /// <summary>
    /// Parse keybindings from all .ini files in mod's work directory
    /// </summary>
    Task<List<ModKeybinding>> ParseKeybindingsAsync(string modSha);
}

public class ModKeybindingService : IModKeybindingService
{
    private readonly IProfilePathService _profilePathService;

    public ModKeybindingService(IProfilePathService profilePathService)
    {
        _profilePathService = profilePathService;
    }

    public async Task<List<ModKeybinding>> ParseKeybindingsAsync(string modSha)
    {
        var keybindings = new List<ModKeybinding>();

        try
        {
            var cacheModsPath = _profilePathService.CacheModsDirectory;

            // Check for active mod folder first (without DISABLED- prefix)
            var modWorkDir = Path.Combine(cacheModsPath, modSha);

            // If not found, check for disabled mod folder (with DISABLED- prefix)
            if (!Directory.Exists(modWorkDir))
            {
                modWorkDir = Path.Combine(cacheModsPath, $"DISABLED-{modSha}");

                if (!Directory.Exists(modWorkDir))
                {
                    return keybindings; // No cache exists (neither loaded nor disabled), return empty list
                }
            }

            // Find all .ini files in mod directory and subdirectories
            var iniFiles = Directory.GetFiles(modWorkDir, "*.ini", SearchOption.AllDirectories);

            foreach (var iniFile in iniFiles)
            {
                var fileKeybindings = await ParseIniFileAsync(iniFile);
                keybindings.AddRange(fileKeybindings);
            }

            // Merge duplicate keys
            keybindings = MergeDuplicateKeys(keybindings);

            // Sort keybindings in a logical order
            keybindings = SortKeybindings(keybindings);
        }
        catch (Exception ex)
        {
            // Log error but don't throw - return empty list if parsing fails
            Console.WriteLine($"Error parsing keybindings for mod {modSha}: {ex.Message}");
        }

        return keybindings;
    }

    /// <summary>
    /// Merge keybindings with the same key by combining their descriptions and types
    /// </summary>
    private List<ModKeybinding> MergeDuplicateKeys(List<ModKeybinding> keybindings)
    {
        var mergedDict = new Dictionary<string, ModKeybinding>();

        foreach (var binding in keybindings)
        {
            var key = binding.Key.ToLower();

            if (mergedDict.ContainsKey(key))
            {
                // Merge with existing entry
                var existing = mergedDict[key];

                // Merge descriptions with "/" separator only if different
                if (!string.IsNullOrEmpty(binding.Description) &&
                    !string.Equals(existing.Description, binding.Description, StringComparison.OrdinalIgnoreCase))
                {
                    existing.Description += " / " + binding.Description;
                }

                // Merge types with "/" separator only if different
                if (!string.IsNullOrEmpty(binding.Type) &&
                    !string.Equals(existing.Type, binding.Type, StringComparison.OrdinalIgnoreCase))
                {
                    existing.Type = string.IsNullOrEmpty(existing.Type)
                        ? binding.Type
                        : existing.Type + " / " + binding.Type;
                }

                // Keep the first variable and cycle values (or merge if needed)
                if (string.IsNullOrEmpty(existing.Variable) && !string.IsNullOrEmpty(binding.Variable))
                {
                    existing.Variable = binding.Variable;
                    existing.CycleValues = binding.CycleValues;
                }
            }
            else
            {
                // Add new entry
                mergedDict[key] = binding;
            }
        }

        return mergedDict.Values.ToList();
    }

    /// <summary>
    /// Sort keybindings in a logical order for display
    /// Priority: Numbers (0-9) → Letters (A-Z) → Function keys (F1-F12) → Arrow keys → Special keys
    /// </summary>
    private List<ModKeybinding> SortKeybindings(List<ModKeybinding> keybindings)
    {
        return keybindings.OrderBy(k => GetKeySortPriority(k.Key))
                         .ThenBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
                         .ToList();
    }

    /// <summary>
    /// Get sort priority for a key (lower number = higher priority)
    /// </summary>
    private int GetKeySortPriority(string key)
    {
        var keyUpper = key.ToUpper();

        // Priority 1: Number keys (0-9)
        if (keyUpper.Length == 1 && char.IsDigit(keyUpper[0]))
            return 1;

        // Priority 2: Letter keys (A-Z)
        if (keyUpper.Length == 1 && char.IsLetter(keyUpper[0]))
            return 2;

        // Priority 3: Numpad keys
        if (keyUpper.StartsWith("VK_NUMPAD") || keyUpper.Contains("NUMPAD"))
            return 3;

        // Priority 4: Function keys (F1-F12)
        if (keyUpper.StartsWith("VK_F") || (keyUpper.StartsWith("F") && keyUpper.Length <= 3))
            return 4;

        // Priority 5: Arrow keys
        if (keyUpper.Contains("UP") || keyUpper.Contains("DOWN") ||
            keyUpper.Contains("LEFT") || keyUpper.Contains("RIGHT"))
            return 5;

        // Priority 6: Common modifier/special keys
        if (keyUpper.Contains("SHIFT") || keyUpper.Contains("CTRL") || keyUpper.Contains("CONTROL") ||
            keyUpper.Contains("ALT") || keyUpper.Contains("SPACE") || keyUpper.Contains("TAB") ||
            keyUpper.Contains("ENTER") || keyUpper.Contains("RETURN") || keyUpper.Contains("ESC"))
            return 6;

        // Priority 7: Navigation keys
        if (keyUpper.Contains("HOME") || keyUpper.Contains("END") ||
            keyUpper.Contains("PRIOR") || keyUpper.Contains("NEXT") || keyUpper.Contains("PAGE"))
            return 7;

        // Priority 8: Edit keys
        if (keyUpper.Contains("INSERT") || keyUpper.Contains("DELETE") || keyUpper.Contains("BACK"))
            return 8;

        // Priority 9: Special characters and symbols
        if (keyUpper.Length == 1 && !char.IsLetterOrDigit(keyUpper[0]))
            return 9;

        // Priority 10: Everything else
        return 10;
    }

    private async Task<List<ModKeybinding>> ParseIniFileAsync(string filePath)
    {
        var keybindings = new List<ModKeybinding>();

        try
        {
            var lines = await File.ReadAllLinesAsync(filePath);
            string? currentSection = null;
            var currentKeybinding = new ModKeybinding();

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith(";"))
                    continue;

                // Check for section header
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    // Save previous keybinding if it was a key section
                    if (currentSection != null && currentSection.StartsWith("Key", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrEmpty(currentKeybinding.Key))
                    {
                        keybindings.Add(currentKeybinding);
                    }

                    currentSection = trimmedLine.Trim('[', ']');
                    currentKeybinding = new ModKeybinding
                    {
                        SectionName = currentSection
                    };
                    continue;
                }

                // Parse key-value pairs within sections
                if (currentSection != null && currentSection.StartsWith("Key", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmedLine.Split('=', 2);
                    if (parts.Length != 2) continue;

                    var key = parts[0].Trim().ToLower();
                    var value = parts[1].Trim();

                    switch (key)
                    {
                        case "key":
                            currentKeybinding.Key = value;
                            currentKeybinding.KeyDisplay = ConvertKeyToDisplay(value);
                            currentKeybinding.Description = ExtractDescription(currentSection);
                            break;
                        case "type":
                            currentKeybinding.Type = value;
                            break;
                    }

                    // Check if this line assigns to a variable (e.g., "$color = 0,1,2,3")
                    if (trimmedLine.Contains("$"))
                    {
                        var match = Regex.Match(trimmedLine, @"\$(\w+)\s*=\s*(.+)");
                        if (match.Success)
                        {
                            currentKeybinding.Variable = "$" + match.Groups[1].Value;
                            currentKeybinding.CycleValues = match.Groups[2].Value;
                        }
                    }
                }
            }

            // Don't forget the last keybinding
            if (currentSection != null && currentSection.StartsWith("Key", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(currentKeybinding.Key))
            {
                keybindings.Add(currentKeybinding);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing ini file {filePath}: {ex.Message}");
        }

        return keybindings;
    }

    /// <summary>
    /// Convert technical key names to user-friendly display names
    /// Supports combination keys like "ctrl a", "shift VK_F1", etc.
    /// Filters out no_ prefixed modifiers (e.g., "no_ctrl no_shift alt j" becomes "Alt + J")
    /// </summary>
    private string ConvertKeyToDisplay(string key)
    {
        // Check if this is a combination key (contains space)
        if (key.Contains(" "))
        {
            var parts = key.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            // Filter out no_ prefixed modifiers (they specify keys that should NOT be pressed)
            var displayParts = parts
                .Where(part => !part.StartsWith("no_", StringComparison.OrdinalIgnoreCase))
                .Select(part => ConvertSingleKeyToDisplay(part))
                .ToArray();
            return string.Join(" + ", displayParts);
        }

        return ConvertSingleKeyToDisplay(key);
    }

    /// <summary>
    /// Convert a single key to display name
    /// </summary>
    private string ConvertSingleKeyToDisplay(string key)
    {
        var keyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Mouse buttons
            { "VK_LBUTTON", "L-Click" },
            { "VK_RBUTTON", "R-Click" },
            { "VK_MBUTTON", "M-Click" },
            { "VK_XBUTTON1", "Mouse 4" },
            { "VK_XBUTTON2", "Mouse 5" },

            // Arrow keys
            { "VK_UP", "↑" },
            { "VK_DOWN", "↓" },
            { "VK_LEFT", "←" },
            { "VK_RIGHT", "→" },

            // Common keys
            { "VK_SPACE", "Space" },
            { "VK_RETURN", "Enter" },
            { "VK_ESCAPE", "Esc" },
            { "VK_TAB", "Tab" },
            { "VK_SHIFT", "Shift" },
            { "VK_LSHIFT", "L-Shift" },
            { "VK_RSHIFT", "R-Shift" },
            { "VK_CONTROL", "Ctrl" },
            { "VK_LCONTROL", "L-Ctrl" },
            { "VK_RCONTROL", "R-Ctrl" },
            { "VK_ALT", "Alt" },
            { "VK_LMENU", "L-Alt" },
            { "VK_RMENU", "R-Alt" },
            { "VK_BACK", "Backspace" },
            { "VK_DELETE", "Delete" },
            { "VK_INSERT", "Insert" },
            { "VK_HOME", "Home" },
            { "VK_END", "End" },
            { "VK_PRIOR", "Page Up" },
            { "VK_NEXT", "Page Down" },

            // Function keys
            { "VK_F1", "F1" },
            { "VK_F2", "F2" },
            { "VK_F3", "F3" },
            { "VK_F4", "F4" },
            { "VK_F5", "F5" },
            { "VK_F6", "F6" },
            { "VK_F7", "F7" },
            { "VK_F8", "F8" },
            { "VK_F9", "F9" },
            { "VK_F10", "F10" },
            { "VK_F11", "F11" },
            { "VK_F12", "F12" },

            // Numpad keys
            { "VK_NUMPAD0", "Num 0" },
            { "VK_NUMPAD1", "Num 1" },
            { "VK_NUMPAD2", "Num 2" },
            { "VK_NUMPAD3", "Num 3" },
            { "VK_NUMPAD4", "Num 4" },
            { "VK_NUMPAD5", "Num 5" },
            { "VK_NUMPAD6", "Num 6" },
            { "VK_NUMPAD7", "Num 7" },
            { "VK_NUMPAD8", "Num 8" },
            { "VK_NUMPAD9", "Num 9" },
            { "VK_ADD", "Num +" },
            { "VK_SUBTRACT", "Num -" },
            { "VK_MULTIPLY", "Num *" },
            { "VK_DIVIDE", "Num /" },
            { "VK_DECIMAL", "Num ." },

            // Other special keys
            { "VK_CAPITAL", "Caps" },
            { "VK_NUMLOCK", "NumLock" },
            { "VK_SCROLL", "ScrLock" },
            { "VK_PAUSE", "Pause" },
            { "VK_SNAPSHOT", "PrtScn" },
        };

        return keyMap.TryGetValue(key, out var displayName) ? displayName : key.ToUpper();
    }

    /// <summary>
    /// Extract a user-friendly description from the section name
    /// e.g., "KeyBodyColor" -> "Body Color"
    /// </summary>
    private string ExtractDescription(string sectionName)
    {
        if (!sectionName.StartsWith("Key", StringComparison.OrdinalIgnoreCase))
            return sectionName;

        // Remove "Key" prefix
        var description = sectionName.Substring(3);

        // Insert spaces before capital letters
        description = Regex.Replace(description, "([a-z])([A-Z])", "$1 $2");

        return description;
    }
}
