using System.Runtime.InteropServices;
using System.Threading;

namespace D3dxSkinManager.Infrastructure;

/// <summary>
/// Enforces ONE running instance PER INSTALL. The app is not multi-instance-safe: each profile is a
/// single-writer SQLite DB, the mod-cache <c>FileOperationPlanner</c> only serializes ops WITHIN one
/// process, and the WebView2 user-data folder is a single OS lock — a 2nd instance corrupts state.
///
/// A 2nd launch broadcasts an activation message and exits; the running instance's main form catches
/// it (see <c>OptimizedForm.WndProcHook</c> / <c>ApplicationHost.ActivateMainWindow</c>) and comes to
/// the foreground. Keyed by the install directory so DISTINCT installs may run side-by-side.
///
/// Runs FIRST in <see cref="ApplicationBootstrapper.Run"/> — before the WebView2 prewarm that takes the
/// user-data lock.
/// </summary>
public static class SingleInstanceGuard
{
    // Held for the process lifetime; the OS releases it when the process exits (normal quit or an
    // updater-triggered restart), so the relaunched instance re-acquires cleanly.
    private static Mutex? _mutex;

    /// <summary>
    /// Registered window message the running instance listens for. Per-install (folded into the string
    /// via <see cref="ChannelKey"/>), so activating one install never foregrounds another. 0 until
    /// <see cref="TryAcquire"/> runs.
    /// </summary>
    public static uint ActivateMessageId { get; private set; }

    /// <summary>
    /// Stable per-install key for the mutex + activation message. Same install dir → same key; different
    /// installs → different keys. Normalized case-insensitive + trailing-separator-insensitive so
    /// <c>C:\App</c>, <c>C:\App\</c> and <c>c:\app</c> collapse to one instance. FNV-1a keeps it to hex
    /// chars (a raw path is not a valid mutex/message name).
    /// </summary>
    public static string ChannelKey(string? installDir)
    {
        var norm = (installDir ?? string.Empty).TrimEnd('\\', '/').ToLowerInvariant();
        uint h = 2166136261; // FNV-1a 32-bit — deterministic, no crypto needed
        foreach (var c in norm)
        {
            h ^= c;
            h *= 16777619;
        }
        return h.ToString("x8");
    }

    public static string MutexName(string key) => $"Local\\D3dxSkinManager.instance.{key}";

    public static string MessageName(string key) => $"D3dxSkinManager.activate.{key}";

    /// <summary>
    /// True = we are the first/only instance (mutex now held for the process lifetime). False = another
    /// instance already owns this install; the caller should <see cref="BroadcastActivate"/> then exit.
    /// A mutex failure fails OPEN (returns true) — never block a legitimate launch on an OS hiccup.
    /// </summary>
    public static bool TryAcquire(string? installDir)
    {
        var key = ChannelKey(installDir);
        ActivateMessageId = RegisterWindowMessage(MessageName(key));
        try
        {
            _mutex = new Mutex(initiallyOwned: true, MutexName(key), out bool createdNew);
            if (!createdNew)
            {
                _mutex.Dispose();
                _mutex = null;
                return false;
            }
            return true;
        }
        catch
        {
            return true; // fail open — an OS mutex error must not stop the app from starting
        }
    }

    /// <summary>2nd instance → tell the running instance to come to the foreground.</summary>
    public static void BroadcastActivate()
    {
        if (ActivateMessageId != 0)
        {
            PostMessage(HWND_BROADCAST, ActivateMessageId, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private static readonly IntPtr HWND_BROADCAST = new(0xffff);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
}
