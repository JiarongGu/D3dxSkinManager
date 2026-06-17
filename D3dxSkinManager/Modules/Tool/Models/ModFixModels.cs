namespace D3dxSkinManager.Modules.Tool.Models;

/// <summary>
/// A registered fix tool in the per-profile fix-tool library. Each tool is a FOLDER under
/// {profile}/fixtools/{Id} (a fix can be multiple files — a script plus its deps, or an exe plus
/// DLLs); <see cref="EntryFile"/> is the runnable entry inside that folder. Persisted in fixtools.json.
/// </summary>
public class ModFixTool
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    /// <summary>Runnable entry, relative to the tool's folder (e.g. "fix.exe"). Empty = unresolved.</summary>
    public string EntryFile { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool RecompressDefault { get; set; } = true;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Candidate runnable files (relative paths) inside the tool — offered to the user to choose the
    /// single entry when it can't be auto-resolved (zero or several executables). Empty once resolved.
    /// </summary>
    public List<string> Candidates { get; set; } = new();

    /// <summary>
    /// Absolute path to the resolved entry, or null when unresolved (user must pick from Candidates
    /// before this tool can run). Computed on read, not persisted.
    /// </summary>
    public string? EntryPath { get; set; }
}

/// <summary>
/// Request to run a mod-fixing script (e.g. a 3DMigoto hash-fix script) against one or more mods.
/// A "fix" is a modder-distributed .py / .exe / .bat that rewrites the mod's .ini hashes (and other
/// assets) so it keeps rendering after a game update. The script is executed with its working
/// directory set to the mod's extracted cache folder — exactly how these scripts expect to run.
/// </summary>
public class ModFixRequest
{
    /// <summary>Absolute path to the fix script/executable the user selected (.py, .exe, .bat, .cmd).</summary>
    public string ScriptPath { get; set; } = string.Empty;

    /// <summary>
    /// Mod IDs to run the fix against. Empty/null = run against ALL mods in the profile.
    /// </summary>
    public List<string> ModIds { get; set; } = new();

    /// <summary>
    /// After a successful fix, re-compress the (mutated) cache back into the mod archive so the change
    /// survives the next unload→reload cycle (which re-extracts from the archive). Default true.
    /// </summary>
    public bool RecompressAfter { get; set; } = true;
}

/// <summary>Overall result of a mod-fix run.</summary>
public class ModFixResult
{
    public int Total { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public bool Cancelled { get; set; }
    public List<ModFixItemResult> Results { get; set; } = new();
}

/// <summary>Per-mod result of a fix run.</summary>
public class ModFixItemResult
{
    public string ModId { get; set; } = string.Empty;
    public string ModName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public bool Skipped { get; set; }
    /// <summary>Process exit code (null if the script never started, e.g. skipped).</summary>
    public int? ExitCode { get; set; }
    /// <summary>Tail of combined stdout/stderr captured from the script (trimmed for transport).</summary>
    public string? Output { get; set; }
    public string? Error { get; set; }
}
