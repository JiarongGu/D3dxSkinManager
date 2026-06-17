namespace D3dxSkinManager.Modules.Tool.Models;

/// <summary>One runnable entry of a fix tool: its relative name and resolved absolute path.</summary>
public class ModFixEntry
{
    /// <summary>Relative path inside the tool folder (e.g. "fix.exe", "scripts/fix.py").</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Absolute path used by the runner.</summary>
    public string Path { get; set; } = string.Empty;
}

/// <summary>
/// A registered fix tool ("toolset") in the per-profile fix-tool library. Each tool is a top-level
/// entry under {profile}/fixtools/: a loose executable, or a FOLDER (a fix can be multiple files). A
/// toolset can expose MULTIPLE runnable entries (<see cref="Entries"/>); when none are explicitly
/// chosen, a lone executable is auto-resolved, otherwise the user picks from <see cref="Candidates"/>.
/// </summary>
public class ModFixTool
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool RecompressDefault { get; set; } = true;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The runnable entries the user can launch. Empty = unresolved (pick from Candidates).</summary>
    public List<ModFixEntry> Entries { get; set; } = new();

    /// <summary>All runnable files (relative) inside the tool — the pool to choose entries from.</summary>
    public List<string> Candidates { get; set; } = new();
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
