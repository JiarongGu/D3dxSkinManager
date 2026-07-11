using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Service for parsing and managing mod keybindings from .ini files
/// </summary>
public interface IModKeybindingService
{
    /// <summary>
    /// Parse keybindings from all .ini files in mod's work directory
    /// </summary>
    Task<List<ModKeybinding>> ParseKeybindingsAsync(string modId);

    /// <summary>
    /// Rebind a key in the mod's .ini files: every <c>[Key*]</c> section whose <c>key = oldKey</c> is
    /// rewritten to <c>key = newKey</c> (line-level edit, comments/order preserved), then the cache is
    /// recompressed back into the archive so the change persists. Returns the number of lines changed.
    /// </summary>
    Task<int> UpdateKeybindingAsync(string modId, string oldKey, string newKey);

    /// <summary>
    /// Persist the display order of keybindings as <paramref name="orderedKeys"/> (the <c>key =</c>
    /// values in the desired order) in the mod's <c>Metadata</c> JSON. Stored as metadata — not by
    /// reordering .ini sections — because a single mod's keybindings can span MULTIPLE .ini files, so a
    /// global order can't be expressed by per-file section order. <see cref="ParseKeybindingsAsync"/>
    /// applies this saved order. Functionally inert for 3DMigoto; purely organisational.
    /// </summary>
    Task ReorderKeybindingsAsync(string modId, List<string> orderedKeys);
}

public class ModKeybindingService : IModKeybindingService
{
    private const string OrderMetadataKey = "keybindingOrder";

    private readonly IModCacheService _cacheService;
    private readonly IModArchiveService _archiveService;
    private readonly IModOperationQueue _operationQueue;
    private readonly IModRepository _repository;

    public ModKeybindingService(
        IModCacheService cacheService,
        IModArchiveService archiveService,
        IModOperationQueue operationQueue,
        IModRepository repository)
    {
        _cacheService = cacheService;
        _archiveService = archiveService;
        _operationQueue = operationQueue;
        _repository = repository;
    }

    /// <summary>Read the saved keybinding order from a mod's Metadata JSON (empty if none/invalid).</summary>
    private static List<string> ReadOrder(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata)) return new();
        try
        {
            if (JsonNode.Parse(metadata) is JsonObject obj && obj[OrderMetadataKey] is JsonArray arr)
                return arr.Where(x => x != null).Select(x => x!.GetValue<string>()).ToList();
        }
        catch { /* malformed metadata — ignore */ }
        return new();
    }

    /// <summary>Write the keybinding order into a Metadata JSON string, preserving other fields.</summary>
    private static string WriteOrder(string? metadata, List<string> order)
    {
        JsonObject obj;
        try { obj = JsonNode.Parse(string.IsNullOrWhiteSpace(metadata) ? "{}" : metadata) as JsonObject ?? new JsonObject(); }
        catch { obj = new JsonObject(); }
        obj[OrderMetadataKey] = new JsonArray(order.Select(s => JsonValue.Create(s)).ToArray<JsonNode?>());
        return obj.ToJsonString();
    }

    public Task<int> UpdateKeybindingAsync(string modId, string oldKey, string newKey)
    {
        if (string.IsNullOrWhiteSpace(oldKey) || string.IsNullOrWhiteSpace(newKey))
            throw new ArgumentException("oldKey and newKey are required");

        // Per-mod lock so the edit + recompress can't race a load/unload/fix on the same mod.
        return _operationQueue.EnqueueAsync(modId, async () =>
        {
            var cacheDir = _cacheService.GetCachePath(modId);
            if (cacheDir == null)
            {
                throw new OperationException("MOD_NOT_EXTRACTED", "id", modId);
            }

            var changed = 0;
            var oldVal = oldKey.Trim();
            var newVal = newKey.Trim();
            var changedFiles = new List<string>();

            foreach (var iniFile in Directory.GetFiles(cacheDir, "*.ini", SearchOption.AllDirectories)
                         .Where(p => !IniParser.IsDisabledPath(Path.GetRelativePath(cacheDir, p))))
            {
                var lines = await File.ReadAllLinesAsync(iniFile).ConfigureAwait(false);
                var inKeySection = false;
                var fileChanged = false;

                for (var i = 0; i < lines.Length; i++)
                {
                    var trimmed = lines[i].Trim();
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        inKeySection = trimmed.Trim('[', ']').StartsWith("Key", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }
                    if (!inKeySection || IniParser.IsCommentLine(trimmed)) continue;

                    // Match a `key = <value>` assignment. The value is compared with any inline comment
                    // stripped (the parse strips them too), and the comment is preserved on rewrite.
                    var m = Regex.Match(lines[i], @"^(\s*key\s*=\s*)(.*?)(\s*(?:[;；].*)?)$", RegexOptions.IgnoreCase);
                    if (m.Success && string.Equals(m.Groups[2].Value.Trim(), oldVal, StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = m.Groups[1].Value + newVal + m.Groups[3].Value;
                        fileChanged = true;
                        changed++;
                    }
                }

                if (fileChanged)
                {
                    // Cache .ini edit is safe under the per-mod lock (no concurrent op on this mod).
                    await File.WriteAllLinesAsync(iniFile, lines).ConfigureAwait(false);
                    changedFiles.Add(iniFile);
                }
            }

            if (changed == 0)
            {
                throw new OperationException("KEYBINDING_NOT_FOUND", "key", oldVal);
            }

            // Persist FAST: patch only the changed .ini(s) inside the archive (no full recompress).
            foreach (var file in changedFiles)
            {
                var entryPath = Path.GetRelativePath(cacheDir, file).Replace('\\', '/');
                await _archiveService.UpdateFileInArchiveAsync(modId, file, entryPath).ConfigureAwait(false);
            }
            return changed;
        });
    }

    public async Task ReorderKeybindingsAsync(string modId, List<string> orderedKeys)
    {
        if (orderedKeys == null || orderedKeys.Count == 0) return;

        // Persist the order in the mod's Metadata JSON (works across multiple .ini files, unlike .ini
        // section order). ParseKeybindingsAsync applies it.
        var entity = await _repository.GetByIdAsync(modId).ConfigureAwait(false);
        if (entity == null) throw new OperationException("MOD_NOT_FOUND", "id", modId);
        entity.Metadata = WriteOrder(entity.Metadata, orderedKeys);
        await _repository.UpdateAsync(entity).ConfigureAwait(false);
    }

    /// <summary>Apply the saved display order (from Metadata) to a merged keybinding list (stable; unknown keys keep place).</summary>
    private static List<ModKeybinding> ApplySavedOrder(List<ModKeybinding> keybindings, List<string> order)
    {
        if (order.Count == 0) return keybindings;
        var rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < order.Count; i++)
            if (!rank.ContainsKey(order[i].Trim())) rank[order[i].Trim()] = i;
        return keybindings
            .OrderBy(k => rank.TryGetValue(k.Key.Trim(), out var r) ? r : int.MaxValue)
            .ToList();
    }

    public async Task<List<ModKeybinding>> ParseKeybindingsAsync(string modId)
    {
        var keybindings = new List<ModKeybinding>();

        try
        {
            // Resolve the extracted cache dir (active {id} or DISABLED-{id})
            var modWorkDir = _cacheService.GetCachePath(modId);
            if (modWorkDir == null)
            {
                return keybindings; // No cache exists (neither loaded nor disabled), return empty list
            }

            // Find all .ini files in mod directory and subdirectories
            // Skip disabled files/folders (DISABLED*.ini, disabled subdirs) — they're inactive in-game.
            var iniFiles = Directory.GetFiles(modWorkDir, "*.ini", SearchOption.AllDirectories)
                .Where(p => !IniParser.IsDisabledPath(Path.GetRelativePath(modWorkDir, p))).ToArray();

            foreach (var iniFile in iniFiles)
            {
                var fileKeybindings = await ParseIniFileAsync(iniFile);
                keybindings.AddRange(fileKeybindings);
            }

            // Merge duplicate keys (preserves first-seen file order), then apply the user's saved display
            // order from Metadata (ReorderKeybindingsAsync). Keys not in the saved order keep their place.
            keybindings = MergeDuplicateKeys(keybindings);
            var entity = await _repository.GetByIdAsync(modId).ConfigureAwait(false);
            keybindings = ApplySavedOrder(keybindings, ReadOrder(entity?.Metadata));
        }
        catch (Exception ex)
        {
            // Log error but don't throw - return empty list if parsing fails
            Console.WriteLine($"Error parsing keybindings for mod {modId}: {ex.Message}");
        }

        return keybindings;
    }

    /// <summary>
    /// Merge keybindings with the same key by combining their descriptions and types
    /// </summary>
    private List<ModKeybinding> MergeDuplicateKeys(List<ModKeybinding> keybindings)
    {
        // Order-stable: a result list preserves first-seen (file/section) order; the dict is only a
        // lookup. This order IS the display order (no re-sort), so manual reorder is honoured.
        var result = new List<ModKeybinding>();
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

                // Union the extra key lines (a merged section may carry controller alternates).
                for (var i = 0; i < binding.AdditionalKeys.Count; i++)
                {
                    if (!existing.AdditionalKeys.Contains(binding.AdditionalKeys[i], StringComparer.OrdinalIgnoreCase))
                    {
                        existing.AdditionalKeys.Add(binding.AdditionalKeys[i]);
                        existing.AdditionalKeyDisplays.Add(
                            i < binding.AdditionalKeyDisplays.Count ? binding.AdditionalKeyDisplays[i] : binding.AdditionalKeys[i]);
                    }
                }
            }
            else
            {
                // Add new entry (record order on first sight).
                mergedDict[key] = binding;
                result.Add(binding);
            }
        }

        return result;
    }

    private async Task<List<ModKeybinding>> ParseIniFileAsync(string filePath)
    {
        var keybindings = new List<ModKeybinding>();

        try
        {
            // Shared tolerant parse (both comment chars, control-flow-safe, inline comments stripped)
            // — see Core.Helpers.IniParser + .claude/knowledge/3dmigoto-ini-interface.md.
            var lines = await File.ReadAllLinesAsync(filePath);
            var doc = IniParser.Parse(lines);

            foreach (var section in doc.Sections)
            {
                if (!section.Name.StartsWith("Key", StringComparison.OrdinalIgnoreCase)) continue;

                var binding = new ModKeybinding { SectionName = section.Name };
                foreach (var entry in section.Entries)
                {
                    if (entry.Key == null || entry.Value == null) continue;

                    if (entry.Key.Equals("key", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrEmpty(binding.Key))
                        {
                            binding.Key = entry.Value;
                            binding.KeyDisplay = ConvertKeyToDisplay(entry.Value);
                            binding.Description = ExtractDescription(section.Name);
                        }
                        else
                        {
                            // A [Key*] section may carry MULTIPLE `key =` lines (keyboard +
                            // controller share state, per the 3DMigoto key doc) — keep every one.
                            binding.AdditionalKeys.Add(entry.Value);
                            binding.AdditionalKeyDisplays.Add(ConvertKeyToDisplay(entry.Value));
                        }
                    }
                    else if (entry.Key.Equals("type", StringComparison.OrdinalIgnoreCase))
                    {
                        binding.Type = entry.Value;
                    }
                    else if (entry.Key.StartsWith('$'))
                    {
                        // The section's cycle assignment (e.g. `$color = 0,1,2,3`). Matching on the
                        // KEY keeps `condition = $x == 1` lines from being misread as cycle vars.
                        binding.Variable = entry.Key;
                        binding.CycleValues = entry.Value;
                    }
                }

                if (!string.IsNullOrEmpty(binding.Key)) keybindings.Add(binding);
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
