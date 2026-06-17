using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Tool.Models;

namespace D3dxSkinManager.Modules.Tool.Services;

/// <summary>
/// Per-profile fix-tool library. The {profile}/fixtools/ FOLDER is the source of truth: each top-level
/// entry is one fix tool — either a loose executable (.exe/.bat/.cmd/.py) or a subfolder. Listing scans
/// the folder, so dropping a tool in makes it appear (with default info) and deleting it removes it; no
/// separate registry to drift. For a folder tool with exactly one executable inside, that entry is used
/// automatically; with zero or several, the entry is left unresolved until the user picks one (the
/// choice is saved as a marker file inside the folder, keeping it folder-authoritative). Game-agnostic.
/// </summary>
public interface IModFixToolService
{
    Task<List<ModFixTool>> GetAllAsync();

    /// <summary>Import a fix tool by copying a source file OR folder into a new fixtools/{name} folder.</summary>
    Task<ModFixTool> ImportAsync(string name, string sourcePath, bool isFolder, string? entryFileName = null, string? description = null);

    /// <summary>Delete a fix tool (id = its top-level name) — removes the folder or the loose file.</summary>
    Task DeleteAsync(string id);

    /// <summary>Set/override which file inside a folder tool is the runnable entry (persists a marker).</summary>
    Task SetEntryAsync(string id, string relativeEntry);

    /// <summary>Resolve a tool's absolute entry path (for the runner). Throws if unresolved/missing.</summary>
    Task<string> GetEntryPathAsync(string id);
}

public class ModFixToolService : IModFixToolService
{
    private readonly IProfilePathService _profilePaths;
    private readonly ILogHelper _logger;
    // Entry auto-detection preference: self-contained exe first, then batch, then python.
    private static readonly string[] EntryExtPriority = { ".exe", ".bat", ".cmd", ".py" };
    // Marker file (inside a folder tool) recording the user's chosen entry; kept out of candidate lists.
    private const string EntryMarker = ".fixentry";

    public ModFixToolService(IProfilePathService profilePaths, ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _logger = logger;
    }

    public Task<List<ModFixTool>> GetAllAsync()
    {
        var root = _profilePaths.FixToolsDirectory;
        var tools = new List<ModFixTool>();
        if (!Directory.Exists(root)) return Task.FromResult(tools);

        // Top-level loose executables — each is a single-file tool (entry = the file itself).
        foreach (var file in Directory.GetFiles(root))
        {
            if (!IsRunnable(file)) continue;
            var fileName = Path.GetFileName(file);
            tools.Add(new ModFixTool
            {
                Id = fileName,
                Name = Path.GetFileNameWithoutExtension(fileName),
                EntryFile = fileName,
                EntryPath = file,
                RecompressDefault = true,
            });
        }

        // Top-level folders — each is a (possibly multi-file) tool; resolve or defer the entry.
        foreach (var dir in Directory.GetDirectories(root))
        {
            var name = Path.GetFileName(dir);
            var candidates = RunnableCandidates(dir);
            var entry = ResolveEntry(dir, candidates);
            tools.Add(new ModFixTool
            {
                Id = name,
                Name = name,
                EntryFile = entry ?? string.Empty,
                EntryPath = entry != null ? Path.Combine(dir, entry) : null,
                Candidates = candidates,
                RecompressDefault = true,
            });
        }

        tools.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(tools);
    }

    public Task<ModFixTool> ImportAsync(string name, string sourcePath, bool isFolder, string? entryFileName = null, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new OperationException("FIX_TOOL_NAME_REQUIRED");

        var folderName = UniqueFolderName(Sanitize(name));
        var toolDir = Path.Combine(_profilePaths.FixToolsDirectory, folderName);

        if (isFolder)
        {
            if (!Directory.Exists(sourcePath))
                throw new OperationException("FIX_TOOL_SOURCE_NOT_FOUND", "path", sourcePath);
            CopyDirectory(sourcePath, toolDir);
            // Entry is resolved lazily on read (single exe → auto; else user picks). Honor an explicit choice.
            if (!string.IsNullOrEmpty(entryFileName))
                WriteMarker(toolDir, entryFileName);
        }
        else
        {
            if (!File.Exists(sourcePath))
                throw new OperationException("FIX_TOOL_SOURCE_NOT_FOUND", "path", sourcePath);
            if (!IsRunnable(sourcePath))
                throw new OperationException("FIX_TOOL_NO_ENTRY");
            Directory.CreateDirectory(toolDir);
            File.Copy(sourcePath, Path.Combine(toolDir, Path.GetFileName(sourcePath)), overwrite: true);
        }

        _logger.Info($"[ModFixTool] Imported fix tool '{folderName}'", "ModFixToolService");

        var candidates = RunnableCandidates(toolDir);
        var entry = ResolveEntry(toolDir, candidates);
        return Task.FromResult(new ModFixTool
        {
            Id = folderName,
            Name = folderName,
            EntryFile = entry ?? string.Empty,
            EntryPath = entry != null ? Path.Combine(toolDir, entry) : null,
            Candidates = candidates,
            RecompressDefault = true,
            Description = description,
        });
    }

    public Task DeleteAsync(string id)
    {
        var path = Path.Combine(_profilePaths.FixToolsDirectory, id);
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            else if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) { _logger.Warn($"[ModFixTool] Failed to delete '{id}': {ex.Message}", "ModFixToolService"); }
        return Task.CompletedTask;
    }

    public Task SetEntryAsync(string id, string relativeEntry)
    {
        var toolDir = Path.Combine(_profilePaths.FixToolsDirectory, id);
        if (!Directory.Exists(toolDir))
            throw new OperationException("FIX_TOOL_NOT_FOUND", "id", id);
        if (!File.Exists(Path.Combine(toolDir, relativeEntry)))
            throw new OperationException("FIX_TOOL_NO_ENTRY");
        WriteMarker(toolDir, relativeEntry);
        return Task.CompletedTask;
    }

    public Task<string> GetEntryPathAsync(string id)
    {
        var path = Path.Combine(_profilePaths.FixToolsDirectory, id);
        if (File.Exists(path) && IsRunnable(path)) return Task.FromResult(path); // loose single-file tool
        if (Directory.Exists(path))
        {
            var entry = ResolveEntry(path, RunnableCandidates(path));
            if (entry != null) return Task.FromResult(Path.Combine(path, entry));
        }
        throw new OperationException("FIX_TOOL_NO_ENTRY");
    }

    // ---- helpers ----

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
    /// Resolve a folder tool's entry: an explicit marker choice wins; else a single runnable is used;
    /// else null (unresolved — the user must pick from candidates).
    /// </summary>
    private static string? ResolveEntry(string toolDir, List<string> candidates)
    {
        var markerPath = Path.Combine(toolDir, EntryMarker);
        if (File.Exists(markerPath))
        {
            var chosen = File.ReadAllText(markerPath).Trim();
            if (!string.IsNullOrEmpty(chosen) && File.Exists(Path.Combine(toolDir, chosen)))
                return chosen;
        }
        // Tier-based: take the highest-priority extension that's present (exe → bat/cmd → py). If exactly
        // one file of that tier exists, it's the entry (a lone .exe wins over helper .py files); if
        // several, it's ambiguous → leave unresolved for the user to pick.
        foreach (var ext in EntryExtPriority)
        {
            var ofExt = candidates.Where(c => string.Equals(Path.GetExtension(c), ext, StringComparison.OrdinalIgnoreCase)).ToList();
            if (ofExt.Count == 1) return ofExt[0];
            if (ofExt.Count > 1) return null;
        }
        return null;
    }

    private static void WriteMarker(string toolDir, string relativeEntry)
        => File.WriteAllText(Path.Combine(toolDir, EntryMarker), relativeEntry.Trim());

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(clean) ? "Fix" : clean;
    }

    private string UniqueFolderName(string baseName)
    {
        var root = _profilePaths.FixToolsDirectory;
        var candidate = baseName;
        var i = 2;
        while (Directory.Exists(Path.Combine(root, candidate)) || File.Exists(Path.Combine(root, candidate)))
            candidate = $"{baseName} ({i++})";
        return candidate;
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, dest));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(source, dest), overwrite: true);
    }
}
