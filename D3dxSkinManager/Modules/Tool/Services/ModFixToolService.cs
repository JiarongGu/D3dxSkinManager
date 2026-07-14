using System.Text.Json;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Tool.Models;

namespace D3dxSkinManager.Modules.Tool.Services;

/// <summary>
/// Per-profile fix-tool library. The {profile}/fixtools/ FOLDER is the source of truth: each top-level
/// entry is one fix tool ("toolset") — either a loose executable (.exe/.bat/.cmd/.py) or a subfolder.
/// Listing scans the folder, so dropping a tool in makes it appear (default info) and deleting it
/// removes it; no separate registry to drift. A toolset can expose MULTIPLE runnable entries; with a
/// single lone executable the entry auto-resolves, otherwise the user picks (the choice persists as a
/// marker file inside the folder, keeping it folder-authoritative). Game-agnostic.
/// </summary>
public interface IModFixToolService
{
    Task<List<ModFixTool>> GetAllAsync();

    /// <summary>Import a fix tool by copying a source file OR folder into a new fixtools/{name} folder.</summary>
    Task<ModFixTool> ImportAsync(string name, string sourcePath, bool isFolder, string? description = null);

    /// <summary>Delete a fix tool (id = its top-level name) — removes the folder or the loose file.</summary>
    Task DeleteAsync(string id);

    /// <summary>Rename a fix tool (folder tool only; loose-file tools are named by their file).
    /// Returns the new id (sanitized + uniquified).</summary>
    Task<string> RenameAsync(string id, string newName);

    /// <summary>
    /// Set which files inside a folder tool are its runnable entries (persists a marker). Pass an empty
    /// list to clear the choice and fall back to auto-resolution.
    /// </summary>
    Task SetEntriesAsync(string id, List<string> relativeEntries);

    /// <summary>Enable/disable a tool (disabled = hidden from the mod "Fix" menu, kept in the library).</summary>
    Task SetEnabledAsync(string id, bool enabled);

    /// <summary>Set (or clear, when alias is empty) the friendly display name of one entry inside a tool.</summary>
    Task SetEntryAliasAsync(string id, string entryName, string? alias);
}

public class ModFixToolService : IModFixToolService
{
    private readonly IProfilePathService _profilePaths;
    private readonly IGlobalPathService _globalPaths;
    private readonly ILogHelper _logger;
    // Entry auto-detection preference: self-contained exe first, then batch, then python.
    private static readonly string[] EntryExtPriority = { ".exe", ".bat", ".cmd", ".py" };
    // Marker file (inside a folder tool) recording the user's chosen entries (one relative path per line).
    private const string EntryMarker = ".fixentry";
    // Sidecar JSON holding per-tool metadata that the folder alone can't express: enabled state + entry
    // display-name aliases. Folder tool → inside the folder; loose-file tool → "{filename}.fixmeta" beside it.
    private const string MetaMarker = ".fixmeta";
    private static readonly JsonSerializerOptions MetaJson = new() { WriteIndented = true };

    public ModFixToolService(IProfilePathService profilePaths, IGlobalPathService globalPaths, ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _globalPaths = globalPaths;
        _logger = logger;
    }

    // Fix tools are PER PROFILE ({profile}/fixtools) — different games need different tool sets.
    // The global {data}/fixtools only remains as a legacy seed source (see EnsureSeeded).
    private string Root => _profilePaths.FixToolsDirectory;

    /// <summary>
    /// One-time seed: when this profile has never had a fixtools dir but the legacy SHARED
    /// {data}/fixtools holds tools (pre-2026-07 layout), copy them in so existing users keep their
    /// library. Dir-exists is the "already seeded" marker — deleting every tool later won't re-seed.
    /// </summary>
    private void EnsureSeeded()
    {
        var root = Root;
        if (Directory.Exists(root)) return;

        var legacy = _globalPaths.FixToolsDirectory;
        Directory.CreateDirectory(root);
        if (!Directory.Exists(legacy)) return;

        try
        {
            foreach (var file in Directory.GetFiles(legacy))
                File.Copy(file, Path.Combine(root, Path.GetFileName(file)), overwrite: false);
            foreach (var dir in Directory.GetDirectories(legacy))
                FileUtilities.CopyDirectory(dir, Path.Combine(root, Path.GetFileName(dir)));
            _logger.Info($"[ModFixTool] Seeded profile fixtools from legacy shared folder ({legacy})", "ModFixToolService");
        }
        catch (Exception ex)
        {
            _logger.Warn($"[ModFixTool] Legacy fixtools seed failed: {ex.Message}", "ModFixToolService");
        }
    }

    public Task<List<ModFixTool>> GetAllAsync()
    {
        EnsureSeeded();
        var root = Root;
        var tools = new List<ModFixTool>();
        if (!Directory.Exists(root)) return Task.FromResult(tools);

        // Top-level loose executables — each is a tool with itself as the only entry.
        foreach (var file in Directory.GetFiles(root))
        {
            if (!IsRunnable(file)) continue;
            var fileName = Path.GetFileName(file);
            var meta = ReadMeta(fileName);
            tools.Add(new ModFixTool
            {
                Id = fileName,
                Name = Path.GetFileNameWithoutExtension(fileName),
                Entries = new List<ModFixEntry> { new() { Name = fileName, Path = file, DisplayName = Alias(meta, fileName) } },
                RecompressDefault = true,
                Enabled = meta.Enabled,
            });
        }

        // Top-level folders — resolve (or defer) one or more entries.
        foreach (var dir in Directory.GetDirectories(root))
        {
            var name = Path.GetFileName(dir);
            var meta = ReadMeta(name);
            var candidates = RunnableCandidates(dir);
            var entries = ResolveEntries(dir, candidates)
                .Select(rel => new ModFixEntry { Name = rel, Path = Path.Combine(dir, rel), DisplayName = Alias(meta, rel) })
                .ToList();
            tools.Add(new ModFixTool
            {
                Id = name,
                Name = name,
                Entries = entries,
                Candidates = candidates,
                RecompressDefault = true,
                Enabled = meta.Enabled,
            });
        }

        tools.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(tools);
    }

    public Task<ModFixTool> ImportAsync(string name, string sourcePath, bool isFolder, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new OperationException("FIX_TOOL_NAME_REQUIRED");
        EnsureSeeded();

        var folderName = UniqueFolderName(Sanitize(name));
        var toolDir = Path.Combine(Root,folderName);

        // Detect file vs folder from the path itself (the `isFolder` arg is only a hint — a dropped
        // path doesn't say which, and the file picker/folder picker both yield a valid path).
        _ = isFolder;
        if (Directory.Exists(sourcePath))
        {
            FileUtilities.CopyDirectory(sourcePath, toolDir);
        }
        else if (File.Exists(sourcePath))
        {
            if (!IsRunnable(sourcePath))
                throw new OperationException("FIX_TOOL_NO_ENTRY");
            Directory.CreateDirectory(toolDir);
            File.Copy(sourcePath, Path.Combine(toolDir, Path.GetFileName(sourcePath)), overwrite: true);
        }
        else
        {
            throw new OperationException("FIX_TOOL_SOURCE_NOT_FOUND", "path", sourcePath);
        }

        _logger.Info($"[ModFixTool] Imported fix tool '{folderName}'", "ModFixToolService");

        var candidates = RunnableCandidates(toolDir);
        var entries = ResolveEntries(toolDir, candidates)
            .Select(rel => new ModFixEntry { Name = rel, Path = Path.Combine(toolDir, rel) })
            .ToList();
        return Task.FromResult(new ModFixTool
        {
            Id = folderName,
            Name = folderName,
            Entries = entries,
            Candidates = candidates,
            RecompressDefault = true,
            Description = description,
        });
    }

    public Task DeleteAsync(string id)
    {
        var path = Path.Combine(Root,id);
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            else if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) { _logger.Warn($"[ModFixTool] Failed to delete '{id}': {ex.Message}", "ModFixToolService"); }
        return Task.CompletedTask;
    }

    public Task<string> RenameAsync(string id, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new OperationException("FIX_TOOL_NAME_REQUIRED");

        var src = Path.Combine(Root, id);
        if (!Directory.Exists(src))
        {
            // Loose-file tools are named by their file; only folder tools can be renamed.
            throw new OperationException("FIX_TOOL_RENAME_FOLDER_ONLY");
        }

        var target = UniqueFolderName(Sanitize(newName));
        if (string.Equals(target, id, StringComparison.Ordinal)) return Task.FromResult(id);

        Directory.Move(src, Path.Combine(Root, target));
        _logger.Info($"[ModFixTool] Renamed fix tool '{id}' → '{target}'", "ModFixToolService");
        return Task.FromResult(target);
    }

    public Task SetEntriesAsync(string id, List<string> relativeEntries)
    {
        var toolDir = Path.Combine(Root,id);
        if (!Directory.Exists(toolDir))
            throw new OperationException("FIX_TOOL_NOT_FOUND", "id", id);

        var markerPath = Path.Combine(toolDir, EntryMarker);
        var valid = (relativeEntries ?? new List<string>())
            .Select(e => e?.Trim() ?? string.Empty)
            .Where(e => e.Length > 0 && File.Exists(Path.Combine(toolDir, e)))
            .Distinct()
            .ToList();

        if (valid.Count == 0)
        {
            // Clear the choice → fall back to auto-resolution.
            try { if (File.Exists(markerPath)) File.Delete(markerPath); } catch { }
        }
        else
        {
            File.WriteAllLines(markerPath, valid);
        }
        return Task.CompletedTask;
    }

    public Task SetEnabledAsync(string id, bool enabled)
    {
        EnsureToolExists(id);
        var meta = ReadMeta(id);
        meta.Enabled = enabled;
        WriteMeta(id, meta);
        return Task.CompletedTask;
    }

    public Task SetEntryAliasAsync(string id, string entryName, string? alias)
    {
        EnsureToolExists(id);
        var key = (entryName ?? string.Empty).Trim();
        if (key.Length == 0) return Task.CompletedTask;

        var meta = ReadMeta(id);
        var clean = alias?.Trim();
        if (string.IsNullOrEmpty(clean)) meta.Aliases.Remove(key);
        else meta.Aliases[key] = clean;
        WriteMeta(id, meta);
        return Task.CompletedTask;
    }

    // ---- helpers ----

    private void EnsureToolExists(string id)
    {
        var path = Path.Combine(Root, id);
        if (!Directory.Exists(path) && !File.Exists(path))
            throw new OperationException("FIX_TOOL_NOT_FOUND", "id", id);
    }

    /// <summary>Resolve the .fixmeta sidecar path: inside a folder tool, or "{file}.fixmeta" beside a loose tool.</summary>
    private string MetaPath(string id)
    {
        var toolPath = Path.Combine(Root, id);
        return Directory.Exists(toolPath)
            ? Path.Combine(toolPath, MetaMarker)
            : Path.Combine(Root, id + MetaMarker);
    }

    private FixMeta ReadMeta(string id)
    {
        var path = MetaPath(id);
        if (!File.Exists(path)) return new FixMeta();
        try { return JsonSerializer.Deserialize<FixMeta>(File.ReadAllText(path)) ?? new FixMeta(); }
        catch { return new FixMeta(); }
    }

    private void WriteMeta(string id, FixMeta meta)
    {
        var path = MetaPath(id);
        try
        {
            // Keep the folder clean: an all-default meta (enabled, no aliases) needs no file.
            if (meta.Enabled && meta.Aliases.Count == 0)
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }
            File.WriteAllText(path, JsonSerializer.Serialize(meta, MetaJson));
        }
        catch (Exception ex) { _logger.Warn($"[ModFixTool] Failed to write meta for '{id}': {ex.Message}", "ModFixToolService"); }
    }

    private static string? Alias(FixMeta meta, string entryName)
        => meta.Aliases.TryGetValue(entryName, out var a) && !string.IsNullOrWhiteSpace(a) ? a : null;

    /// <summary>Per-tool sidecar metadata the folder can't express: enabled state + entry display aliases.</summary>
    private sealed class FixMeta
    {
        public bool Enabled { get; set; } = true;
        public Dictionary<string, string> Aliases { get; set; } = new();
    }

    private static bool IsRunnable(string file)
        => EntryExtPriority.Contains(Path.GetExtension(file).ToLowerInvariant());

    /// <summary>All runnable files inside a tool folder, as relative paths (marker excluded).</summary>
    private static List<string> RunnableCandidates(string toolDir)
        => Directory.GetFiles(toolDir, "*", SearchOption.AllDirectories)
            .Where(IsRunnable)
            .Select(f => Path.GetRelativePath(toolDir, f))
            .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Resolve a folder tool's entries: explicit marker choices win (one per line); else, if a single
    /// executable can be auto-detected (lone exe beats helper .py), use it; else none (user must pick).
    /// </summary>
    private static List<string> ResolveEntries(string toolDir, List<string> candidates)
    {
        var markerPath = Path.Combine(toolDir, EntryMarker);
        if (File.Exists(markerPath))
        {
            var chosen = File.ReadAllLines(markerPath)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && File.Exists(Path.Combine(toolDir, l)))
                .Distinct()
                .ToList();
            if (chosen.Count > 0) return chosen;
        }

        // Auto: take the highest-priority extension present; use it only if exactly one such file.
        foreach (var ext in EntryExtPriority)
        {
            var ofExt = candidates.Where(c => string.Equals(Path.GetExtension(c), ext, StringComparison.OrdinalIgnoreCase)).ToList();
            if (ofExt.Count == 1) return ofExt;
            if (ofExt.Count > 1) return new List<string>();
        }
        return new List<string>();
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(clean) ? "Fix" : clean;
    }

    private string UniqueFolderName(string baseName)
    {
        var root = Root;
        var candidate = baseName;
        var i = 2;
        while (Directory.Exists(Path.Combine(root, candidate)) || File.Exists(Path.Combine(root, candidate)))
            candidate = $"{baseName} ({i++})";
        return candidate;
    }

}
